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
@property (atomic) BOOL isGameActive;
@property (nonatomic, copy) NSString *trackedAppName;
- (void)start;
- (void)stop;
- (void)checkActiveWindow;
- (double)consumePeakFrametime;
@end

static BOOL IsGameApplication(NSRunningApplication *app, SCWindow *targetWindow) {
    if (!app) return NO;
    if (app.processIdentifier == getpid()) return NO;

    NSString *bundleId = (app.bundleIdentifier ?: @"").lowercaseString;
    NSString *appName = (app.localizedName ?: @"").lowercaseString;
    NSString *bundlePath = (app.bundleURL.path ?: @"").lowercaseString;
    NSString *execPath = (app.executableURL.path ?: @"").lowercaseString;

    // 1. Explicit Exclusions: System apps, browsers, dev tools, productivity, communication
    static NSArray<NSString *> *excludedPrefixes = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        excludedPrefixes = @[
            @"com.apple.finder",
            @"com.apple.dock",
            @"com.apple.systempreferences",
            @"com.apple.activitymonitor",
            @"com.apple.terminal",
            @"com.apple.safari",
            @"com.apple.preview",
            @"com.apple.textedit",
            @"com.apple.notes",
            @"com.apple.reminders",
            @"com.apple.mail",
            @"com.apple.music",
            @"com.apple.podcasts",
            @"com.apple.tv",
            @"com.apple.calculator",
            @"com.apple.dt.xcode",
            @"com.apple.quicktimeplayerx",
            @"com.google.chrome",
            @"org.mozilla.firefox",
            @"com.brave.browser",
            @"company.thebrowser.arc",
            @"com.microsoft.edgemac",
            @"com.microsoft.vscode",
            @"com.jetbrains.",
            @"com.tinyspeck.slackmacgap",
            @"com.hnc.discord",
            @"ru.keepcoder.telegram",
            @"net.whatsapp.whatsapp",
            @"com.spotify.client",
            @"com.sublimetext.",
            @"com.postmanlabs.mac",
            @"com.github.wez.wezterm",
            @"com.mitchellh.ghostty",
            @"com.googlecode.iterm2"
        ];
    });

    for (NSString *prefix in excludedPrefixes) {
        if ([bundleId hasPrefix:prefix] || [bundleId containsString:prefix]) {
            return NO;
        }
    }

    // 2. Check macOS Official App Category: public.app-category.games
    if (app.bundleURL) {
        NSBundle *bundle = [NSBundle bundleWithURL:app.bundleURL];
        if (bundle) {
            NSString *category = [bundle objectForInfoDictionaryKey:@"LSApplicationCategoryType"];
            if ([category isKindOfClass:[NSString class]] &&
                [category.lowercaseString containsString:@"games"]) {
                return YES;
            }
        }
    }

    // 3. Known Game Stores, Launchers, and Game Folders
    if ([bundlePath containsString:@"/steamapps/"] ||
        [bundlePath containsString:@"/steam/"] ||
        [bundlePath containsString:@"/epic games/"] ||
        [bundlePath containsString:@"/gog galaxy/"] ||
        [bundlePath containsString:@"/whisky/"] ||
        [bundlePath containsString:@"/crossover/"] ||
        [bundlePath containsString:@"/heroic/"] ||
        [bundlePath containsString:@"/games/"] ||
        [bundlePath containsString:@"/jeux/"] ||
        [execPath containsString:@"/steamapps/"] ||
        [execPath containsString:@"/whisky/"] ||
        [execPath containsString:@"/crossover/"]) {
        return YES;
    }

    // 4. Known Game Publishers, Engines, and Platforms
    static NSArray<NSString *> *gameKeywords = nil;
    static dispatch_once_t kwToken;
    dispatch_once(&kwToken, ^{
        gameKeywords = @[
            @"blizzard", @"warcraft", @"starcraft", @"diablo", @"overwatch", @"hearthstone",
            @"riotgames", @"leagueoflegends", @"valorant",
            @"ea.mac", @"electronicarts", @"origin",
            @"feralinteractive", @"aspyr",
            @"valve", @"dota", @"cs2", @"counter-strike",
            @"larian", @"baldursgate", @"divinity",
            @"unity", @"unreal", @"godot",
            @"roblox", @"minecraft", @"mojang",
            @"wine64", @"wine", @"wineserver", @"gptk", @"d3d",
            @"rpcs3", @"ryujinx", @"dolphin", @"pcsx2", @"retroarch", @"ppsspp", @"duckstation",
            @"finalfantasy", @"residentevil", @"deathstranding", @"cyberpunk", @"hades",
            @"no man's sky", @"nomanssky", @"liesofp", @"tomb raider", @"civilization"
        ];
    });

    for (NSString *kw in gameKeywords) {
        if ([bundleId containsString:kw] || [appName containsString:kw] || [execPath containsString:kw]) {
            return YES;
        }
    }

    // 5. Full-Screen Detection:
    // If an application's window covers the entire main display screen and is NOT a desktop app
    if (targetWindow) {
        NSScreen *mainScreen = [NSScreen mainScreen];
        if (mainScreen) {
            NSRect screenFrame = mainScreen.frame;
            CGRect winFrame = targetWindow.frame;
            if (fabs(winFrame.size.width - screenFrame.size.width) <= 20 &&
                fabs(winFrame.size.height - screenFrame.size.height) <= 40) {
                return YES;
            }
        }
    }

    return NO;
}

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
        _queue = dispatch_queue_create("com.cherryhud.fpsmonitor", DISPATCH_QUEUE_SERIAL);
        _lock = OS_UNFAIR_LOCK_INIT;
        _trackedAppName = @"";
        _currentFps = 0.0;
        _currentFrametime = 0.0;
        _intervalPeakFrametime = 0.0;
        _currentPid = 0;
        _tsHead = 0;
        _tsCount = 0;
        _prevFrameTime = 0;
        _isGameActive = NO;
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
            // Register for instant workspace app activation events (0ms latency!)
            [[[NSWorkspace sharedWorkspace] notificationCenter] addObserver:self
                                                                   selector:@selector(onAppActivated:)
                                                                       name:NSWorkspaceDidActivateApplicationNotification
                                                                     object:nil];

            [self checkActiveWindow];
            self->_pollTimer = [NSTimer scheduledTimerWithTimeInterval:0.1
                                                                target:self
                                                              selector:@selector(checkActiveWindow)
                                                              userInfo:nil
                                                               repeats:YES];
        });
    }
}

- (void)onAppActivated:(NSNotification *)note {
    dispatch_async(dispatch_get_main_queue(), ^{
        [self checkActiveWindow];
    });
}

- (void)stop {
    @synchronized (self) {
        if (!_isRunning) return;
        _isRunning = NO;

        dispatch_async(dispatch_get_main_queue(), ^{
            [[[NSWorkspace sharedWorkspace] notificationCenter] removeObserver:self];
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
        self.isGameActive = NO;
    }
}

static BOOL IsExcludedApp(NSRunningApplication *app) {
    if (!app) return YES;
    if (app.processIdentifier == getpid()) return YES;

    NSString *bundleId = (app.bundleIdentifier ?: @"").lowercaseString;
    static NSArray<NSString *> *excludedPrefixes = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        excludedPrefixes = @[
            @"com.apple.finder",
            @"com.apple.dock",
            @"com.apple.systempreferences",
            @"com.apple.activitymonitor",
            @"com.apple.terminal",
            @"com.apple.safari",
            @"com.apple.preview",
            @"com.apple.textedit",
            @"com.apple.notes",
            @"com.apple.reminders",
            @"com.apple.mail",
            @"com.apple.music",
            @"com.apple.podcasts",
            @"com.apple.tv",
            @"com.apple.calculator",
            @"com.apple.dt.xcode",
            @"com.apple.quicktimeplayerx",
            @"com.google.chrome",
            @"org.mozilla.firefox",
            @"com.brave.browser",
            @"company.thebrowser.arc",
            @"com.microsoft.edgemac",
            @"com.microsoft.vscode",
            @"com.jetbrains.",
            @"com.tinyspeck.slackmacgap",
            @"com.hnc.discord",
            @"ru.keepcoder.telegram",
            @"net.whatsapp.whatsapp",
            @"com.spotify.client",
            @"com.sublimetext.",
            @"com.postmanlabs.mac",
            @"com.github.wez.wezterm",
            @"com.mitchellh.ghostty",
            @"com.googlecode.iterm2"
        ];
    });

    for (NSString *prefix in excludedPrefixes) {
        if ([bundleId hasPrefix:prefix] || [bundleId containsString:prefix]) {
            return YES;
        }
    }
    return NO;
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

    NSString *appName = frontApp.localizedName ?: @"";
    @synchronized (self) {
        self.trackedAppName = appName;
    }

    // 1. FAST-PATH: Synchronous check (0.001ms)
    BOOL isKnownGame = IsGameApplication(frontApp, nil);

    if (!isKnownGame) {
        if (IsExcludedApp(frontApp)) {
            // Definitively NOT a game (Finder, Safari, Rider, Terminal, etc.)
            self.isGameActive = NO;
            if (self->_stream != nil) {
                [self->_stream stopCaptureWithCompletionHandler:nil];
                self->_stream = nil;
            }
            self->_currentPid = 0;
            os_unfair_lock_lock(&self->_lock);
            self.currentFps = 0.0;
            self.currentFrametime = 0.0;
            self->_intervalPeakFrametime = 0.0;
            self->_tsCount = 0;
            self->_prevFrameTime = 0;
            os_unfair_lock_unlock(&self->_lock);
            return;
        }
    } else {
        // Definitively a game (WoW, Steam games, etc.) - mark active IMMEDIATELY (0ms)!
        self.isGameActive = YES;
    }

    // 2. Query ScreenCaptureKit to stream the game's window (or verify fullscreen if not a known game)
    [SCShareableContent getShareableContentExcludingDesktopWindows:YES
                                              onScreenWindowsOnly:NO
                                                completionHandler:^(SCShareableContent *content, NSError *error) {
        if (error || !content || !self->_isRunning) {
            // Even if SCShareableContent fails or permission is missing,
            // we PRESERVE self.isGameActive = isKnownGame so the HUD stays visible over the game!
            return;
        }

        SCWindow *targetWindow = nil;
        CGFloat maxArea = 0;
        for (SCWindow *w in content.windows) {
            if (w.owningApplication.processID == frontApp.processIdentifier) {
                CGFloat area = w.frame.size.width * w.frame.size.height;
                if (area > maxArea && area > 100) {
                    maxArea = area;
                    targetWindow = w;
                }
            }
        }

        // Check if frontmost application is a game (verifying window geometry if needed)
        BOOL isGame = isKnownGame || IsGameApplication(frontApp, targetWindow);

        if (!isGame) {
            // NOT a game: Stop capture immediately
            self.isGameActive = NO;
            if (self->_stream != nil) {
                [self->_stream stopCaptureWithCompletionHandler:nil];
                self->_stream = nil;
            }
            self->_currentPid = 0;
            os_unfair_lock_lock(&self->_lock);
            self.currentFps = 0.0;
            self.currentFrametime = 0.0;
            self->_intervalPeakFrametime = 0.0;
            self->_tsCount = 0;
            self->_prevFrameTime = 0;
            os_unfair_lock_unlock(&self->_lock);
            return;
        }

        // Active game confirmed
        self.isGameActive = YES;

        if (frontApp.processIdentifier == self->_currentPid && self->_stream != nil) {
            return; // Already tracking this game stream
        }

        if (!targetWindow) {
            return;
        }

        self->_currentPid = frontApp.processIdentifier;

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

bool FpsMonitor_IsGameActive(void) {
    if (!s_monitor) return false;
    return s_monitor.isGameActive;
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

static BOOL Swizzled_CanBecomeKeyWindow(NSWindow *self, SEL _cmd) {
    if ([self level] == NSScreenSaverWindowLevel) {
        return NO;
    }
    return YES;
}

static BOOL Swizzled_CanBecomeMainWindow(NSWindow *self, SEL _cmd) {
    if ([self level] == NSScreenSaverWindowLevel) {
        return NO;
    }
    return YES;
}

static void NoOp_Activate(id self, SEL _cmd) {
    // Prevent overlay window from activating over games
}

static void NoOp_ActivateIgnoring(id self, SEL _cmd, BOOL flag) {
    // Prevent overlay window from activating over games
}

static void Swizzled_MakeKeyAndOrderFront(NSWindow *self, SEL _cmd, id sender) {
    [self orderFront:sender];
}

static void Swizzled_MakeKeyWindow(NSWindow *self, SEL _cmd) {
    // NO-OP for overlay window
}

void FpsMonitor_ConfigureOverlayWindow(void *viewOrWindowPtr) {
    dispatch_async(dispatch_get_main_queue(), ^{
        // 1. Set app as accessory overlay so clicking it never summons the Dock or Menu Bar
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];

        // 2. Swizzle NSApplication activation methods
        class_replaceMethod([NSApplication class], @selector(activate), (IMP)NoOp_Activate, "v@:");
        class_replaceMethod([NSApplication class], @selector(activateIgnoringOtherApps:), (IMP)NoOp_ActivateIgnoring, "v@:c");

        // 3. Swizzle NSWindow class methods (guarded by window level)
        class_replaceMethod([NSWindow class], @selector(canBecomeKeyWindow), (IMP)Swizzled_CanBecomeKeyWindow, "c@:");
        class_replaceMethod([NSWindow class], @selector(canBecomeMainWindow), (IMP)Swizzled_CanBecomeMainWindow, "c@:");
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
        if (win) {
            [targets addObject:win];
        } else {
            for (NSWindow *w in [NSApp windows]) {
                [targets addObject:w];
            }
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
            [w setAlphaValue:1.0];
            [w setIgnoresMouseEvents:NO];
            [w orderFront:nil];

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

void FpsMonitor_SetOverlayVisible(void *viewOrWindowPtr, bool visible) {
    dispatch_async(dispatch_get_main_queue(), ^{
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
        if (win) {
            [targets addObject:win];
        } else {
            for (NSWindow *w in [NSApp windows]) {
                [targets addObject:w];
            }
        }

        for (NSWindow *w in targets) {
            if (visible) {
                [w setLevel:NSScreenSaverWindowLevel];
                [w setAlphaValue:1.0];
                [w setIgnoresMouseEvents:NO];
                [w orderFront:nil];
            } else {
                [w setAlphaValue:0.0];
                [w setIgnoresMouseEvents:YES];
                [w orderOut:nil];
            }
        }
    });
}

void FpsMonitor_SetApplicationIcon(const char *iconPathUtf8) {
    if (!iconPathUtf8 || strlen(iconPathUtf8) == 0) return;
    NSString *path = [NSString stringWithUTF8String:iconPathUtf8];
    dispatch_async(dispatch_get_main_queue(), ^{
        NSImage *img = [[NSImage alloc] initWithContentsOfFile:path];
        if (img) {
            [NSApp setApplicationIconImage:img];
            NSImageView *iv = [[NSImageView alloc] init];
            [iv setImage:img];
            [[NSApp dockTile] setContentView:iv];
            [[NSApp dockTile] display];
        }
    });
}

void FpsMonitor_ShowAboutWindow(void *windowPtr, const char *iconPathUtf8) {
    // Capture path synchronously to avoid dangling pointer across dispatch_async
    NSString *path = (iconPathUtf8 && strlen(iconPathUtf8) > 0) ? [NSString stringWithUTF8String:iconPathUtf8] : nil;

    dispatch_async(dispatch_get_main_queue(), ^{
        // 1. Show in Dock
        [NSApp setActivationPolicy:NSApplicationActivationPolicyRegular];

        if (path) {
            NSImage *img = [[NSImage alloc] initWithContentsOfFile:path];
            if (img) {
                [NSApp setApplicationIconImage:img];
                NSImageView *iv = [[NSImageView alloc] init];
                [iv setImage:img];
                [[NSApp dockTile] setContentView:iv];
                [[NSApp dockTile] display];
            }
        } else if ([NSApp applicationIconImage] != nil) {
            [[NSApp dockTile] display];
        }

        NSWindow *win = nil;
        if (windowPtr) {
            id obj = (__bridge id)windowPtr;
            if ([obj isKindOfClass:[NSWindow class]]) {
                win = (NSWindow *)obj;
            } else if ([obj isKindOfClass:[NSView class]]) {
                win = [(NSView *)obj window];
            }
        }

        if (win) {
            // Above all other windows (Floating / ScreenSaver level)
            [win setLevel:NSFloatingWindowLevel];
            [win setCollectionBehavior:NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary];
            [win setHidesOnDeactivate:NO];
            [win orderFrontRegardless];
            [win makeKeyAndOrderFront:nil];
        }

        // 2. Bring application forward over all other windows
        [[NSRunningApplication currentApplication] activateWithOptions:0];
    });
}

void FpsMonitor_HideAboutWindow(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        // Return to accessory mode (hide from Dock)
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
    });
}


