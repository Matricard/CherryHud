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
#include <sys/sysctl.h>

#define MAX_TIMESTAMPS 360

__attribute__((constructor))
static void FpsMonitor_LibraryInit(void) {
    @autoreleasepool {
        [[NSProcessInfo processInfo] setProcessName:@"CherryHud"];
    }
}

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

    // 1. Explicit Exclusions: System apps, browsers, dev tools, productivity, launchers, stores
    static NSArray<NSString *> *excludedPrefixes = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        excludedPrefixes = @[
            @"com.apple.",
            @"com.google.",
            @"com.microsoft.",
            @"com.jetbrains.",
            @"com.danpristupov.fork",
            @"dev.zed.zed",
            @"com.sublimetext.",
            @"com.sublimemerge",
            @"com.valvesoftware.steam",
            @"net.battle.app",
            @"com.epicgames.epicgameslauncher",
            @"com.gog.galaxy",
            @"com.heroicgameslauncher",
            @"com.isaacmarovitz.whisky",
            @"org.mozilla.firefox",
            @"com.brave.browser",
            @"company.thebrowser.arc",
            @"com.tinyspeck.slackmacgap",
            @"com.hnc.discord",
            @"ru.keepcoder.telegram",
            @"net.whatsapp.whatsapp",
            @"com.spotify.client",
            @"com.postmanlabs.mac",
            @"com.github.wez.wezterm",
            @"com.mitchellh.ghostty",
            @"com.googlecode.iterm2"
        ];
    });

    // 2. Check macOS Official App Category: public.app-category.games
    // (Checked first so legitimate Apple Arcade / App Store games are recognized)
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

    for (NSString *prefix in excludedPrefixes) {
        if ([bundleId hasPrefix:prefix] || [bundleId containsString:prefix]) {
            return NO;
        }
    }

    // 3. Known Game Stores, Launchers, and Game Folders (specifically games, NOT the launchers themselves)
    if ([bundlePath containsString:@"/steamapps/"] ||
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
    BOOL _isQueryingContent;
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
        _isQueryingContent = NO;
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
        _isQueryingContent = NO;

        dispatch_async(dispatch_get_main_queue(), ^{
            [[[NSWorkspace sharedWorkspace] notificationCenter] addObserver:self
                                                                   selector:@selector(onAppActivated:)
                                                                       name:NSWorkspaceDidActivateApplicationNotification
                                                                     object:nil];

            [self checkActiveWindow];

            // Gentle fallback polling every 1.5s on common runloop modes (never blocks menu tracking)
            self->_pollTimer = [NSTimer timerWithTimeInterval:1.5
                                                       target:self
                                                     selector:@selector(checkActiveWindow)
                                                     userInfo:nil
                                                      repeats:YES];
            [[NSRunLoop mainRunLoop] addTimer:self->_pollTimer forMode:NSRunLoopCommonModes];
        });
    }
}

- (void)onAppActivated:(NSNotification *)note {
    dispatch_async(dispatch_get_main_queue(), ^{
        // Debounce workspace transitions by 150ms to allow WindowServer space animation to complete
        [NSObject cancelPreviousPerformRequestsWithTarget:self selector:@selector(checkActiveWindow) object:nil];
        [self performSelector:@selector(checkActiveWindow) withObject:nil afterDelay:0.15];
    });
}

- (void)stop {
    @synchronized (self) {
        if (!_isRunning) return;
        _isRunning = NO;

        dispatch_async(dispatch_get_main_queue(), ^{
            [NSObject cancelPreviousPerformRequestsWithTarget:self selector:@selector(checkActiveWindow) object:nil];
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
        _isQueryingContent = NO;
    }
}

static BOOL IsExcludedApp(NSRunningApplication *app) {
    if (!app) return YES;
    if (app.processIdentifier == getpid()) return YES;

    // If officially marked as a game, it is never excluded
    if (app.bundleURL) {
        NSBundle *bundle = [NSBundle bundleWithURL:app.bundleURL];
        if (bundle) {
            NSString *category = [bundle objectForInfoDictionaryKey:@"LSApplicationCategoryType"];
            if ([category isKindOfClass:[NSString class]] &&
                [category.lowercaseString containsString:@"games"]) {
                return NO;
            }
        }
    }

    NSString *bundleId = (app.bundleIdentifier ?: @"").lowercaseString;
    static NSArray<NSString *> *excludedPrefixes = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        excludedPrefixes = @[
            @"com.apple.",
            @"com.google.",
            @"com.microsoft.",
            @"com.jetbrains.",
            @"com.danpristupov.fork",
            @"dev.zed.zed",
            @"com.sublimetext.",
            @"com.sublimemerge",
            @"com.valvesoftware.steam",
            @"net.battle.app",
            @"com.epicgames.epicgameslauncher",
            @"com.gog.galaxy",
            @"com.heroicgameslauncher",
            @"com.isaacmarovitz.whisky",
            @"org.mozilla.firefox",
            @"com.brave.browser",
            @"company.thebrowser.arc",
            @"com.tinyspeck.slackmacgap",
            @"com.hnc.discord",
            @"ru.keepcoder.telegram",
            @"net.whatsapp.whatsapp",
            @"com.spotify.client",
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

    // If we are already capturing this exact process and stream is healthy, NO NEED to re-query ScreenCaptureKit!
    if (frontApp.processIdentifier == self->_currentPid && self->_stream != nil) {
        return;
    }

    // Prevent overlapping asynchronous queries to ScreenCaptureKit
    if (_isQueryingContent) {
        return;
    }
    _isQueryingContent = YES;

    // 2. Query ScreenCaptureKit to stream the game's window (or verify fullscreen if not a known game)
    [SCShareableContent getShareableContentExcludingDesktopWindows:YES
                                              onScreenWindowsOnly:NO
                                                completionHandler:^(SCShareableContent *content, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            self->_isQueryingContent = NO;

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
        });
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

bool FpsMonitor_IsLaptop(void) {
    static int s_isLaptop = -1;
    if (s_isLaptop != -1) {
        return s_isLaptop == 1;
    }

    // 1. Check IOKit AppleSmartBattery
    mach_port_t mainPort = kIOMainPortDefault;
    io_service_t batteryService = IOServiceGetMatchingService(mainPort, IOServiceMatching("AppleSmartBattery"));
    if (batteryService != IO_OBJECT_NULL) {
        IOObjectRelease(batteryService);
        s_isLaptop = 1;
        return true;
    }

    // 2. Check IOPowerSources for InternalBattery
    CFTypeRef blob = IOPSCopyPowerSourcesInfo();
    if (blob) {
        CFArrayRef list = IOPSCopyPowerSourcesList(blob);
        if (list) {
            CFIndex count = CFArrayGetCount(list);
            for (CFIndex i = 0; i < count; i++) {
                CFDictionaryRef desc = IOPSGetPowerSourceDescription(blob, CFArrayGetValueAtIndex(list, i));
                if (desc) {
                    NSString *type = (__bridge NSString *)CFDictionaryGetValue(desc, CFSTR(kIOPSTypeKey));
                    if ([type isEqualToString:(__bridge NSString *)CFSTR(kIOPSInternalBatteryType)]) {
                        CFRelease(list);
                        CFRelease(blob);
                        s_isLaptop = 1;
                        return true;
                    }
                }
            }
            CFRelease(list);
        }
        CFRelease(blob);
    }

    // 3. Fallback: check hw.model
    size_t size = 0;
    if (sysctlbyname("hw.model", NULL, &size, NULL, 0) == 0 && size > 0) {
        char *model = malloc(size);
        if (model) {
            if (sysctlbyname("hw.model", model, &size, NULL, 0) == 0) {
                NSString *modelStr = [NSString stringWithUTF8String:model];
                if ([modelStr containsString:@"MacBook"]) {
                    free(model);
                    s_isLaptop = 1;
                    return true;
                }
            }
            free(model);
        }
    }

    s_isLaptop = 0;
    return false;
}

void FpsMonitor_GetBatteryStats(int *outPercent, bool *outIsCharging) {
    if (outPercent) *outPercent = -1;
    if (outIsCharging) *outIsCharging = false;

    if (!FpsMonitor_IsLaptop()) {
        return;
    }

    CFTypeRef blob = IOPSCopyPowerSourcesInfo();
    if (!blob) return;

    CFArrayRef list = IOPSCopyPowerSourcesList(blob);
    if (list) {
        CFIndex count = CFArrayGetCount(list);
        for (CFIndex i = 0; i < count; i++) {
            CFDictionaryRef desc = IOPSGetPowerSourceDescription(blob, CFArrayGetValueAtIndex(list, i));
            if (desc) {
                NSString *type = (__bridge NSString *)CFDictionaryGetValue(desc, CFSTR(kIOPSTypeKey));
                // Only consider internal laptop battery, ignore UPS / external peripherals
                if (type && ![type isEqualToString:(__bridge NSString *)CFSTR(kIOPSInternalBatteryType)]) {
                    continue;
                }

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
                break;
            }
        }
        CFRelease(list);
    }
    CFRelease(blob);
}

void FpsMonitor_ConfigureOverlayWindow(void *viewOrWindowPtr) {
    dispatch_async(dispatch_get_main_queue(), ^{
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];

        NSWindow *win = nil;
        if (viewOrWindowPtr) {
            id obj = (__bridge id)viewOrWindowPtr;
            if ([obj isKindOfClass:[NSWindow class]]) {
                win = (NSWindow *)obj;
            } else if ([obj isKindOfClass:[NSView class]]) {
                win = [(NSView *)obj window];
            }
        }

        if (!win) {
            for (NSWindow *w in [NSApp windows]) {
                if (w) {
                    win = w;
                    break;
                }
            }
        }

        if (!win) return;

        // Elevate window above normal windows and over fullscreen games
        [win setLevel:NSScreenSaverWindowLevel];

        // Auxiliary fullscreen collection behavior:
        // Allows the floating HUD to stay visible on top of full-screen games
        NSWindowCollectionBehavior behavior = NSWindowCollectionBehaviorCanJoinAllSpaces |
                                             NSWindowCollectionBehaviorFullScreenAuxiliary |
                                             NSWindowCollectionBehaviorStationary |
                                             NSWindowCollectionBehaviorIgnoresCycle;
        [win setCollectionBehavior:behavior];

        // Do not hide when switching away from the widget
        [win setHidesOnDeactivate:NO];
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

        if (!win) {
            for (NSWindow *w in [NSApp windows]) {
                if (w && w.level == NSScreenSaverWindowLevel) {
                    win = w;
                    break;
                }
            }
        }

        if (!win) return;

        if (visible) {
            [win setLevel:NSScreenSaverWindowLevel];
            [win setAlphaValue:1.0];
            [win setIgnoresMouseEvents:NO];
            [win orderFront:nil];
        } else {
            [win setAlphaValue:0.0];
            [win setIgnoresMouseEvents:YES];
            [win orderOut:nil];
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

@interface CherryZoomHandler : NSObject
@property (nonatomic, assign) NSWindow *window;
@property (nonatomic, assign) NSRect preZoomFrame;
@property (nonatomic, assign) BOOL isZoomed;
- (void)toggleZoom:(id)sender;
- (void)toggleMiniaturize:(id)sender;
@end

@implementation CherryZoomHandler
- (void)toggleZoom:(id)sender {
    if (!self.window) return;
    NSScreen *screen = [self.window screen] ?: [NSScreen mainScreen];
    if (!screen) return;

    NSRect currentFrame = [self.window frame];
    NSRect vis = [screen visibleFrame];
    CGFloat maxH = vis.size.height;
    CGFloat maxW = round(maxH * (680.0 / 580.0));
    if (maxW > vis.size.width) {
        maxW = vis.size.width;
        maxH = round(maxW * (580.0 / 680.0));
    }

    // Si la fenêtre est déjà à la taille agrandie (ou marquée comme telle)
    if (self.isZoomed || fabs(currentFrame.size.height - maxH) < 8.0) {
        NSRect restoreFrame = self.preZoomFrame;
        if (restoreFrame.size.width < 680.0 || restoreFrame.size.height < 580.0) {
            restoreFrame = NSMakeRect(currentFrame.origin.x, currentFrame.origin.y, 680.0, 580.0);
        }
        [self.window setFrame:restoreFrame display:YES animate:YES];
        self.isZoomed = NO;
    } else {
        self.preZoomFrame = currentFrame;
        CGFloat targetX = round(vis.origin.x + (vis.size.width - maxW) / 2.0);
        CGFloat targetY = round(vis.origin.y + (vis.size.height - maxH) / 2.0);
        NSRect targetFrame = NSMakeRect(targetX, targetY, maxW, maxH);
        [self.window setFrame:targetFrame display:YES animate:YES];
        self.isZoomed = YES;
    }
}

- (void)toggleMiniaturize:(id)sender {
    if (!self.window) return;
    [self.window miniaturize:sender];
}
@end

void FpsMonitor_ShowAboutWindow(void *windowPtr, const char *iconPathUtf8) {
    // Capture path synchronously to avoid dangling pointer across dispatch_async
    NSString *path = (iconPathUtf8 && strlen(iconPathUtf8) > 0) ? [NSString stringWithUTF8String:iconPathUtf8] : nil;

    dispatch_async(dispatch_get_main_queue(), ^{
        // 1. S'assurer que le nom du processus est CherryHud et activation policy regular pour le dock
        [[NSProcessInfo processInfo] setProcessName:@"CherryHud"];
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
            NSString *className = NSStringFromClass([win class]);
            if ([className containsString:@"StatusBar"] || [className containsString:@"StatusItem"] || [win level] == 25) {
                return;
            }
        }

        if (!win) {
            for (NSWindow *w in [NSApp windows]) {
                if (w && [w isVisible]) {
                    NSString *className = NSStringFromClass([w class]);
                    if ([className containsString:@"StatusBar"] || [className containsString:@"StatusItem"] || [w level] == 25 || [w level] == NSScreenSaverWindowLevel) {
                        continue;
                    }
                    win = w;
                    break;
                }
            }
        }

        if (!win) return;

        NSString *winClass = NSStringFromClass([win class]);
        if ([winClass containsString:@"StatusBar"] || [winClass containsString:@"StatusItem"] || [win level] == 25 || [win level] == NSScreenSaverWindowLevel) {
            return;
        }

        if (win) {
            if ([win isMiniaturized]) {
                [win deminiaturize:nil];
                [win makeKeyAndOrderFront:nil];
            }

            // Barre de titre transparente intégrée avec boutons classiques macOS (Fermer, Réduire, Agrandir)
            win.titlebarAppearsTransparent = YES;
            win.titleVisibility = NSWindowTitleHidden;
            win.styleMask |= NSWindowStyleMaskFullSizeContentView | NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable;
            [win setLevel:NSNormalWindowLevel];
            [win setMovableByWindowBackground:YES];

            // Maintien strict du ratio d'aspect (680 x 580) lors du redimensionnement et du zoom (agrandir)
            NSSize baseRatio = NSMakeSize(680.0, 580.0);
            [win setMinSize:baseRatio];
            [win setContentMinSize:baseRatio];
            [win setAspectRatio:baseRatio];
            [win setContentAspectRatio:baseRatio];

            CherryZoomHandler *handler = objc_getAssociatedObject(win, "cherry_zoom_handler");
            if (!handler) {
                handler = [[CherryZoomHandler alloc] init];
                handler.window = win;
                handler.preZoomFrame = [win frame];
                handler.isZoomed = NO;
                objc_setAssociatedObject(win, "cherry_zoom_handler", handler, OBJC_ASSOCIATION_RETAIN_NONATOMIC);
            }

            // Rétablir les 3 boutons classiques macOS avec leur comportement natif
            NSButton *closeBtn = [win standardWindowButton:NSWindowCloseButton];
            if (closeBtn) {
                [closeBtn setHidden:NO];
                [closeBtn setEnabled:YES];
            }
            NSButton *miniBtn = [win standardWindowButton:NSWindowMiniaturizeButton];
            if (miniBtn) {
                [miniBtn setHidden:NO];
                [miniBtn setEnabled:YES];
                [miniBtn setTarget:handler];
                [miniBtn setAction:@selector(toggleMiniaturize:)];
            }
            NSButton *zoomBtn = [win standardWindowButton:NSWindowZoomButton];
            if (zoomBtn) {
                [zoomBtn setHidden:NO];
                [zoomBtn setEnabled:YES];
                [zoomBtn setMenu:nil];
                [zoomBtn setToolTip:nil];
                for (NSTrackingArea *ta in [[zoomBtn trackingAreas] copy]) {
                    [zoomBtn removeTrackingArea:ta];
                }
                [zoomBtn setTarget:handler];
                [zoomBtn setAction:@selector(toggleZoom:)];
            }

            [win setOpaque:NO];
            [win setBackgroundColor:[NSColor clearColor]];
            [win setAppearance:[NSAppearance appearanceNamed:NSAppearanceNameVibrantDark]];

            // Flou translucide sombre, net et hautement transparent pour la fenêtre
            NSView *contentView = [win contentView];
            if (contentView) {
                NSVisualEffectView *contentVev = nil;
                for (NSView *sub in [contentView subviews]) {
                    if ([sub isKindOfClass:[NSVisualEffectView class]]) {
                        contentVev = (NSVisualEffectView *)sub;
                        break;
                    }
                }

                if (!contentVev) {
                    contentVev = [[NSVisualEffectView alloc] initWithFrame:[contentView bounds]];
                    [contentVev setAutoresizingMask:NSViewWidthSizable | NSViewHeightSizable];
                    [contentVev setBlendingMode:NSVisualEffectBlendingModeBehindWindow];
                    [contentVev setMaterial:NSVisualEffectMaterialHUDWindow];
                    [contentVev setState:NSVisualEffectStateActive];
                    [contentVev setAlphaValue:0.45];
                    [contentVev setAppearance:[NSAppearance appearanceNamed:NSAppearanceNameVibrantDark]];
                    [contentView addSubview:contentVev positioned:NSWindowBelow relativeTo:nil];
                } else {
                    [contentVev setAutoresizingMask:NSViewWidthSizable | NSViewHeightSizable];
                    [contentVev setBlendingMode:NSVisualEffectBlendingModeBehindWindow];
                    [contentVev setMaterial:NSVisualEffectMaterialHUDWindow];
                    [contentVev setState:NSVisualEffectStateActive];
                    [contentVev setAlphaValue:0.45];
                    [contentVev setAppearance:[NSAppearance appearanceNamed:NSAppearanceNameVibrantDark]];
                }

                // Assurer que tout autre NSVisualEffectView dans la fenêtre (barre de titre) reste également actif
                NSView *rootView = [contentView superview] ?: contentView;
                NSMutableArray *stack = [NSMutableArray arrayWithObject:rootView];
                while ([stack count] > 0) {
                    NSView *v = [stack lastObject];
                    [stack removeLastObject];
                    if ([v isKindOfClass:[NSVisualEffectView class]] && v != contentVev) {
                        NSVisualEffectView *vev = (NSVisualEffectView *)v;
                        [vev setState:NSVisualEffectStateActive];
                        [vev setAppearance:[NSAppearance appearanceNamed:NSAppearanceNameVibrantDark]];
                    }
                    [stack addObjectsFromArray:[v subviews]];
                }
            }

            [win setLevel:NSNormalWindowLevel];
            // Désactiver le menu popup de disposition/mosaïque (Move & Resize) de macOS Sequoia
            NSWindowCollectionBehavior behavior = NSWindowCollectionBehaviorFullScreenNone;
            behavior |= (NSWindowCollectionBehavior)(1 << 12); // NSWindowCollectionBehaviorFullScreenDisallowsTiling
            [win setCollectionBehavior:behavior];
            [win setHidesOnDeactivate:NO];
            [win orderFrontRegardless];
            [win makeKeyAndOrderFront:nil];
        }

        // 2. Bring application forward over all other windows
        [[NSRunningApplication currentApplication] activateWithOptions:0];
    });
}

void FpsMonitor_MinimizeWindow(void *windowPtr) {
    dispatch_async(dispatch_get_main_queue(), ^{
        NSWindow *win = nil;
        if (windowPtr) {
            id obj = (__bridge id)windowPtr;
            if ([obj isKindOfClass:[NSWindow class]]) {
                win = (NSWindow *)obj;
            } else if ([obj isKindOfClass:[NSView class]]) {
                win = [(NSView *)obj window];
            }
        }
        if (!win) {
            for (NSWindow *w in [NSApp windows]) {
                if (w && [w isVisible]) {
                    NSString *className = NSStringFromClass([w class]);
                    if ([className containsString:@"StatusBar"] || [className containsString:@"StatusItem"] || [w level] == 25 || [w level] == NSScreenSaverWindowLevel) {
                        continue;
                    }
                    win = w;
                    break;
                }
            }
        }
        if (win) {
            [win miniaturize:nil];
        }
    });
}

void FpsMonitor_HideAboutWindow(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        // Return to accessory mode (hide from Dock)
        [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
    });
}


