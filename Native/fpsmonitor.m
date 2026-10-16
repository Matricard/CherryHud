// MIT License - Copyright (c) 2026 Mathieu Ricard
#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <ScreenCaptureKit/ScreenCaptureKit.h>
#import <CoreMedia/CoreMedia.h>
#import <CoreGraphics/CoreGraphics.h>
#import <os/lock.h>

#define MAX_TIMESTAMPS 360

@interface FpsMonitor : NSObject <SCStreamOutput, SCStreamDelegate>
@property (atomic) double currentFps;
@property (nonatomic, copy) NSString *trackedAppName;
- (void)start;
- (void)stop;
- (void)checkActiveWindow;
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
    BOOL _isRunning;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _queue = dispatch_queue_create("com.mactemp.fpsmonitor", DISPATCH_QUEUE_SERIAL);
        _lock = OS_UNFAIR_LOCK_INIT;
        _trackedAppName = @"None";
        _currentFps = 0.0;
        _currentPid = 0;
        _tsHead = 0;
        _tsCount = 0;
    }
    return self;
}

- (void)start {
    @synchronized (self) {
        if (_isRunning) return;
        _isRunning = YES;
        _lastFrameTime = [NSDate timeIntervalSinceReferenceDate];
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
        _tsCount = 0;
    }
}

- (void)checkActiveWindow {
    if (!_isRunning) return;

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    os_unfair_lock_lock(&_lock);
    if (now - _lastFrameTime > 1.0 && self.currentFps > 0) {
        self.currentFps = 0.0;
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
    NSString *appName = frontApp.localizedName ?: @"App";
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

    // 1. CRITICAL: Only count new completed frames!
    // Ignore duplicate/idle frames (SCFrameStatusIdle), blank frames, or cursor-only updates.
    CFArrayRef attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, false);
    if (!attachments || CFArrayGetCount(attachments) == 0) return;

    CFDictionaryRef attachment = (CFDictionaryRef)CFArrayGetValueAtIndex(attachments, 0);
    NSNumber *statusNum = (__bridge NSNumber *)CFDictionaryGetValue(attachment, (__bridge const void *)SCStreamFrameInfoStatus);
    if (statusNum == nil || [statusNum integerValue] != SCFrameStatusComplete) {
        return; // Skip idle / duplicate frames
    }

    os_unfair_lock_lock(&_lock);
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    _lastFrameTime = now;

    // 2. Sliding 1.0-second window of real completed frames
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

- (void)stream:(SCStream *)stream didStopWithError:(NSError *)error {
    @synchronized (self) {
        _stream = nil;
        self.currentFps = 0.0;
        _tsCount = 0;
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

const char* FpsMonitor_GetTrackedAppName(void) {
    if (!s_monitor) return "None";
    NSString *name = s_monitor.trackedAppName ?: @"None";
    [name getCString:s_appNameBuffer maxLength:sizeof(s_appNameBuffer) encoding:NSUTF8StringEncoding];
    return s_appNameBuffer;
}
