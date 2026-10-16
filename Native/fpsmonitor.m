// MIT License - Copyright (c) 2026 Mathieu Ricard
#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreGraphics/CoreGraphics.h>
#import <IOKit/IOKitLib.h>
#import <IOKit/ps/IOPowerSources.h>
#import <IOKit/ps/IOPSKeys.h>
#import <os/lock.h>
#import <objc/runtime.h>

#define MAX_TIMESTAMPS 360

@interface FpsMonitor : NSObject <SCStreamOutput, SCStreamDelegate>
@property (atomic) double currentFps;
@property (atomic) double currentFrametime;
@property (nonatomic, copy) NSString *trackedAppName;
- (void)start;
- (void)stop;
- (void)checkActiveWindow;
- (double)consumePeakFrametime;
@end

@implementation FpsMonitor {
    SCStream *_stream;
    dispatch_queue_t _queue;
    NSTimer *_pollTimer;
    pid_t _currentPid;
    
    os_unfair_lock _lock;
    NSTimeInterval _timestamps[MAX_TIMESTAMPS];
    int _tsHead;
    int _tsCount;
    NSTimeInterval _lastFrameTime;
    NSTimeInterval _prevFrameTime;
    double _intervalPeakFrametime;

    BOOL _isRunning;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _queue = dispatch_queue_create("com.mactemp.fpsmonitor", DISPATCH_QUEUE_SERIAL);
        _lock = OS_UNFAIR_LOCK_INIT;
        _trackedAppName = @"";
        _currentFps = 0.0;
        _currentFrametime = 0.0;
        _intervalPeakFrametime = 0.0;
        _currentPid = 0;
        _tsHead = 0;
        _tsCount = 0;
        _prevFrameTime = 0;
    }
    return self;
}

- (void)start {
    @synchronized (self) {
        if (_isRunning) return;
        _isRunning = YES;
        _lastFrameTime = [NSDate timeIntervalSinceReferenceDate];
        _prevFrameTime = 0;
        _intervalPeakFrametime = 0;
        _tsHead = 0;
        _tsCount = 0;

        dispatch_async(dispatch_get_main_queue(), ^{
            [self checkActiveWindow];
            self->_pollTimer = [NSTimer scheduledTimerWithTimeInterval:1.0
                                                                target:self
                                                              selector:@selector(checkActiveWindow)
                                                              userInfo:nil
                                                               repeats:YES];
        });
    }
}

- (void)stop {
    @synchronized (self) {
        if (!_isRunning) return;
        _isRunning = NO;

        dispatch_async(dispatch_get_main_queue(), ^{
            [self->_pollTimer invalidate];
            self->_pollTimer = nil;
        });

        if (_stream) {
            [_stream stopCaptureWithCompletionHandler:nil];
            _stream = nil;
        }
        _currentPid = 0;
        self.currentFps = 0.0;
        self.currentFrametime = 0.0;
        _intervalPeakFrametime = 0;
        _tsCount = 0;
        _prevFrameTime = 0;
    }
}

- (void)checkActiveWindow {
    if (!_isRunning) return;

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    os_unfair_lock_lock(&_lock);
    if (now - _lastFrameTime > 1.0 && self.currentFps > 0) {
        self.currentFps = 0.0;
        self.currentFrametime = 0.0;
        _intervalPeakFrametime = 0.0;
        _tsCount = 0;
    }
    os_unfair_lock_unlock(&_lock);

    NSRunningApplication *frontApp = [[NSWorkspace sharedWorkspace] frontmostApplication];
    if (!frontApp || frontApp.processIdentifier == getpid()) {
        return;
    }

    if (frontApp.processIdentifier == _currentPid && _stream != nil) {
        return; // Already tracking this application
    }

    _currentPid = frontApp.processIdentifier;
    NSString *appName = frontApp.localizedName ?: @"";
    @synchronized (self) {
        self.trackedAppName = appName;
    }

    [SCShareableContent getShareableContentExcludingDesktopWindows:YES
                                              onScreenWindowsOnly:NO
                                                completionHandler:^(SCShareableContent *content, NSError *error) {
        if (error || !content || !self->_isRunning) {
            return;
        }

        SCWindow *targetWindow = nil;
        CGFloat maxArea = 0;
        for (SCWindow *w in content.windows) {
            if (w.owningApplication.processID == self->_currentPid) {
                CGFloat area = w.frame.size.width * w.frame.size.height;
                if (area > maxArea && area > 100) {
                    maxArea = area;
                    targetWindow = w;
                }
            }
        }

        if (!targetWindow) {
            return;
        }

        SCContentFilter *filter = [[SCContentFilter alloc] initWithDesktopIndependentWindow:targetWindow];
        SCStreamConfiguration *config = [[SCStreamConfiguration alloc] init];
        config.width = 64;
        config.height = 64;
        config.minimumFrameInterval = CMTimeMake(1, 240);
        config.pixelFormat = kCVPixelFormatType_32BGRA;
        config.queueDepth = 3;
        config.showsCursor = NO;

        if (self->_stream) {
            [self->_stream updateContentFilter:filter completionHandler:nil];
            [self->_stream updateConfiguration:config completionHandler:nil];
        } else {
            NSError *streamError = nil;
            self->_stream = [[SCStream alloc] initWithFilter:filter configuration:config delegate:self];
            [self->_stream addStreamOutput:self type:SCStreamOutputTypeScreen sampleHandlerQueue:self->_queue error:&streamError];
            if (!streamError) {
                [self->_stream startCaptureWithCompletionHandler:nil];
            }
        }
    }];
}

#pragma mark - SCStreamOutput

- (void)stream:(SCStream *)stream didOutputSampleBuffer:(CMSampleBufferRef)sampleBuffer ofType:(SCStreamOutputType)type {
    if (type != SCStreamOutputTypeScreen) return;

    // 1. Only count new completed frames!
    CFArrayRef attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, false);
    if (!attachments || CFArrayGetCount(attachments) == 0) return;

    CFDictionaryRef attachment = (CFDictionaryRef)CFArrayGetValueAtIndex(attachments, 0);
    NSNumber *statusNum = (__bridge NSNumber *)CFDictionaryGetValue(attachment, (__bridge const void *)SCStreamFrameInfoStatus);
    if (statusNum == nil || [statusNum integerValue] != SCFrameStatusComplete) {
        return; // Skip idle / duplicate frames
    }

    os_unfair_lock_lock(&_lock);
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];

    // Measure exact frametime in milliseconds between consecutive completed frames
    if (_prevFrameTime > 0) {
        NSTimeInterval delta = now - _prevFrameTime;
        double ftMs = delta * 1000.0;
        if (ftMs >= 0.2 && ftMs <= 500.0) {
            if (ftMs > _intervalPeakFrametime) {
                _intervalPeakFrametime = ftMs;
            }
            self.currentFrametime = ftMs;
        }
    }
    _prevFrameTime = now;
    _lastFrameTime = now;

    // 2. Sliding 1.0-second window of real completed frames for FPS
    _timestamps[_tsHead] = now;
    _tsHead = (_tsHead + 1) % MAX_TIMESTAMPS;
    if (_tsCount < MAX_TIMESTAMPS) {
        _tsCount++;
    }

    // Prune timestamps older than 1.0 second
    NSTimeInterval cutoff = now - 1.0;
    int tail = (_tsHead - _tsCount + MAX_TIMESTAMPS) % MAX_TIMESTAMPS;
    while (_tsCount > 0 && _timestamps[tail] < cutoff) {
        tail = (tail + 1) % MAX_TIMESTAMPS;
        _tsCount--;
    }

    self.currentFps = (double)_tsCount;
    os_unfair_lock_unlock(&_lock);
}

- (double)consumePeakFrametime {
    os_unfair_lock_lock(&_lock);
    double peak = _intervalPeakFrametime;
    _intervalPeakFrametime = self.currentFrametime;
    os_unfair_lock_unlock(&_lock);
    return peak > 0.1 ? peak : self.currentFrametime;
}

- (void)stream:(SCStream *)stream didStopWithError:(NSError *)error {
    @synchronized (self) {
        _stream = nil;
        self.currentFps = 0.0;
        self.currentFrametime = 0.0;
        _intervalPeakFrametime = 0.0;
        _tsCount = 0;
        _prevFrameTime = 0;
    }
}

@end

static FpsMonitor *s_monitor = nil;
static char s_appNameBuffer[256] = {0};

bool FpsMonitor_HasPermission(void) {
    return CGPreflightScreenCaptureAccess();
}

void FpsMonitor_RequestPermission(void) {
    CGRequestScreenCaptureAccess();
}

void FpsMonitor_Start(void) {
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        s_monitor = [[FpsMonitor alloc] init];
    });
    [s_monitor start];
}

void FpsMonitor_Stop(void) {
    if (s_monitor) {
        [s_monitor stop];
    }
}

double FpsMonitor_GetFps(void) {
    if (!s_monitor) return 0.0;
    return s_monitor.currentFps;
}

double FpsMonitor_GetCurrentFrametime(void) {
    if (!s_monitor) return 0.0;
    return s_monitor.currentFrametime;
}

double FpsMonitor_GetPeakFrametime(void) {
    if (!s_monitor) return 0.0;
    return [s_monitor consumePeakFrametime];
}

const char* FpsMonitor_GetTrackedAppName(void) {
    if (!s_monitor) return "";
    NSString *name = s_monitor.trackedAppName ?: @"";
    [name getCString:s_appNameBuffer maxLength:sizeof(s_appNameBuffer) encoding:NSUTF8StringEncoding];
    return s_appNameBuffer;
}

void FpsMonitor_GetGpuStats(double *outUtilization, double *outVramBytes) {
    if (outUtilization) *outUtilization = 0.0;
    if (outVramBytes) *outVramBytes = 0.0;

    CFMutableDictionaryRef match = IOServiceMatching("IOAccelerator");
    io_iterator_t it;
    if (IOServiceGetMatchingServices(kIOMainPortDefault, match, &it) == kIOReturnSuccess) {
        io_service_t serv;
        while ((serv = IOIteratorNext(it))) {
            CFMutableDictionaryRef props = NULL;
            if (IORegistryEntryCreateCFProperties(serv, &props, kCFAllocatorDefault, 0) == kIOReturnSuccess && props) {
                NSDictionary *dict = (__bridge NSDictionary *)props;
                NSDictionary *perf = dict[@"PerformanceStatistics"];
                if (perf) {
                    NSNumber *util = perf[@"Device Utilization %"];
                    NSNumber *vram = perf[@"In use system memory"] ?: perf[@"Alloc system memory"];
                    if (util && outUtilization) {
                        *outUtilization = [util doubleValue];
                    }
                    if (vram && outVramBytes) {
                        *outVramBytes = [vram doubleValue];
                    }
                }
                CFRelease(props);
            }
            IOObjectRelease(serv);
            break;
        }
        IOObjectRelease(it);
    }
}

void FpsMonitor_GetBatteryStats(int *outPercent, bool *outIsCharging) {
    if (outPercent) *outPercent = -1;
    if (outIsCharging) *outIsCharging = false;

    CFTypeRef blob = IOPSCopyPowerSourcesInfo();
    if (!blob) return;

    CFArrayRef list = IOPSCopyPowerSourcesList(blob);
    if (list) {
        if (CFArrayGetCount(list) > 0) {
            CFDictionaryRef desc = IOPSGetPowerSourceDescription(blob, CFArrayGetValueAtIndex(list, 0));
            if (desc) {
                NSNumber *cur = (__bridge NSNumber *)CFDictionaryGetValue(desc, CFSTR(kIOPSCurrentCapacityKey));
                NSNumber *max = (__bridge NSNumber *)CFDictionaryGetValue(desc, CFSTR(kIOPSMaxCapacityKey));
                NSString *state = (__bridge NSString *)CFDictionaryGetValue(desc, CFSTR(kIOPSPowerSourceStateKey));
                NSNumber *charging = (__bridge NSNumber *)CFDictionaryGetValue(desc, CFSTR(kIOPSIsChargingKey));

                if (cur && max && [max intValue] > 0 && outPercent) {
                    *outPercent = (int)(([cur doubleValue] / [max doubleValue]) * 100.0);
                }
                if (outIsCharging) {
                    *outIsCharging = (charging && [charging boolValue]) || [state isEqualToString:(__bridge NSString *)CFSTR(kIOPSACPowerValue)];
                }
            }
        }
        CFRelease(list);
    }
    CFRelease(blob);
}

static BOOL ReturnNO(id self, SEL _cmd) {
    return NO;
}

static void NoOp_Activate(id self, SEL _cmd) {
    // Prevent application from activating over games
}

static void NoOp_ActivateIgnoring(id self, SEL _cmd, BOOL flag) {
    // Prevent application from activating over games
}

static void Swizzled_MakeKeyAndOrderFront(NSWindow *self, SEL _cmd, id sender) {
    [self orderFront:sender];
}

static void Swizzled_MakeKeyWindow(NSWindow *self, SEL _cmd) {
    // NO-OP
}

void FpsMonitor_ConfigureOverlayWindow(void *viewOrWindowPtr) {
    dispatch_async(dispatch_get_main_queue(), ^{
        // 1. Set app as accessory overlay so clicking it never summons the Dock or Menu Bar
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];

        // 2. Swizzle NSApplication activation methods
        class_replaceMethod([NSApplication class], @selector(activate), (IMP)NoOp_Activate, "v@:");
        class_replaceMethod([NSApplication class], @selector(activateIgnoringOtherApps:), (IMP)NoOp_ActivateIgnoring, "v@:c");

        // 3. Swizzle NSWindow class methods
        class_replaceMethod([NSWindow class], @selector(canBecomeKeyWindow), (IMP)ReturnNO, "c@:");
        class_replaceMethod([NSWindow class], @selector(canBecomeMainWindow), (IMP)ReturnNO, "c@:");
        class_replaceMethod([NSWindow class], @selector(makeKeyWindow), (IMP)Swizzled_MakeKeyWindow, "v@:");
        class_replaceMethod([NSWindow class], @selector(makeKeyAndOrderFront:), (IMP)Swizzled_MakeKeyAndOrderFront, "v@:@");

        NSWindow *win = nil;
        if (viewOrWindowPtr) {
            id obj = (__bridge id)viewOrWindowPtr;
            if ([obj isKindOfClass:[NSWindow class]]) {
                win = (NSWindow *)obj;
            } else if ([obj isKindOfClass:[NSView class]]) {
                win = [(NSView *)obj window];
            }
        }

        NSMutableArray<NSWindow *> *targets = [NSMutableArray array];
        if (win) [targets addObject:win];
        for (NSWindow *w in [NSApp windows]) {
            if (![targets containsObject:w]) [targets addObject:w];
        }

        for (NSWindow *w in targets) {
            // Elevate window above macOS menu bar and over fullscreen games
            [w setLevel:NSScreenSaverWindowLevel];

            // Non-activating style mask: prevents mouse clicks from activating the app
            w.styleMask = w.styleMask | NSWindowStyleMaskNonactivatingPanel;

            // Auxiliary fullscreen collection behavior:
            // Allows the floating HUD to stay visible on top of full-screen games!
            NSWindowCollectionBehavior behavior = NSWindowCollectionBehaviorCanJoinAllSpaces |
                                                 NSWindowCollectionBehaviorFullScreenAuxiliary |
                                                 NSWindowCollectionBehaviorStationary |
                                                 NSWindowCollectionBehaviorIgnoresCycle;
            [w setCollectionBehavior:behavior];

            // Do not hide when switching away from the widget
            [w setHidesOnDeactivate:NO];

            Class winClass = [w class];
            if (winClass != [NSWindow class]) {
                class_replaceMethod(winClass, @selector(canBecomeKeyWindow), (IMP)ReturnNO, "c@:");
                class_replaceMethod(winClass, @selector(canBecomeMainWindow), (IMP)ReturnNO, "c@:");
                class_replaceMethod(winClass, @selector(makeKeyWindow), (IMP)Swizzled_MakeKeyWindow, "v@:");
                class_replaceMethod(winClass, @selector(makeKeyAndOrderFront:), (IMP)Swizzled_MakeKeyAndOrderFront, "v@:@");
            }
        }
    });
}

