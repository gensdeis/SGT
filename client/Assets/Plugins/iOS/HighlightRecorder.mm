// 숏게타 iOS highlight recording — ReplayKit RPScreenRecorder 기반.
//
// 동작:
//   1. _ShortGeta_StartRecording: RPScreenRecorder.startCaptureWithHandler
//      → frame 마다 CMSampleBufferRef 를 ring buffer 에 보관 (직전 3초 = ~90 frames)
//   2. _ShortGeta_StopRecording: stopCaptureWithHandler
//   3. _ShortGeta_FlushLastClipPath: ring buffer → AVAssetWriter 로 MP4 export,
//      tmp/highlights/{ts}_{tag}.mp4 경로 반환
//
// ⚠️ 단순화 제약 (iter 2B'):
//   - 음성 캡처 OFF (RPScreenRecorder.microphoneEnabled = NO)
//   - 워터마크 미적용
// TODO Iter 2B''''':
//   - 워터마크: AVAssetWriter 입력 전에 CIImage / CGContext 로 우하단 박스 합성
//   - 음성: RPScreenRecorder.microphoneEnabled = YES + 권한 NSRequestRecordPermission
//   - 권한 거부 시 Available=NO 반환
//   - AVAssetWriter export 비동기 — flushLastClipPath 는 동기 wait (UI block 가능)
//   실제 production 에서는 async + callback 패턴 권장.

#import <ReplayKit/ReplayKit.h>
#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

#define MAX_BUFFER_FRAMES 90
#define BUFFER_SECONDS    3.0

static NSMutableArray<NSValue*> *gFrameRing = nil; // CMSampleBufferRef wrapped
static NSString *gCurrentTag = @"untagged";
static NSString *gLastClipPath = nil;
static BOOL gRecording = NO;

static void EnsureRing() {
    if (gFrameRing == nil) {
        gFrameRing = [NSMutableArray arrayWithCapacity:MAX_BUFFER_FRAMES];
    }
}

extern "C" {

bool _ShortGeta_IsAvailable(void) {
    if (![RPScreenRecorder sharedRecorder].available) {
        return false;
    }
    return true;
}

void _ShortGeta_StartRecording(const char* tag) {
    if (gRecording) return;
    EnsureRing();
    [gFrameRing removeAllObjects];
    gCurrentTag = (tag != NULL) ? [NSString stringWithUTF8String:tag] : @"untagged";

    RPScreenRecorder *rec = [RPScreenRecorder sharedRecorder];
    rec.microphoneEnabled = NO;

    if (@available(iOS 11.0, *)) {
        [rec startCaptureWithHandler:^(CMSampleBufferRef sampleBuffer, RPSampleBufferType bufferType, NSError * _Nullable error) {
            if (error != nil) return;
            if (bufferType != RPSampleBufferTypeVideo) return;
            if (!CMSampleBufferIsValid(sampleBuffer)) return;
            CFRetain(sampleBuffer);
            @synchronized (gFrameRing) {
                [gFrameRing addObject:[NSValue valueWithPointer:sampleBuffer]];
                while (gFrameRing.count > MAX_BUFFER_FRAMES) {
                    NSValue *old = gFrameRing.firstObject;
                    CMSampleBufferRef oldBuf = (CMSampleBufferRef)[old pointerValue];
                    CFRelease(oldBuf);
                    [gFrameRing removeObjectAtIndex:0];
                }
            }
        } completionHandler:^(NSError * _Nullable error) {
            if (error != nil) {
                NSLog(@"[ShortGeta] startCapture error: %@", error);
                return;
            }
            gRecording = YES;
            NSLog(@"[ShortGeta] iOS start tag=%@", gCurrentTag);
        }];
    }
}

void _ShortGeta_StopRecording(void) {
    if (!gRecording) return;
    if (@available(iOS 11.0, *)) {
        [[RPScreenRecorder sharedRecorder] stopCaptureWithHandler:^(NSError * _Nullable error) {
            if (error != nil) NSLog(@"[ShortGeta] stopCapture error: %@", error);
        }];
    }
    gRecording = NO;
}

const char* _ShortGeta_FlushLastClipPath(void) {
    @synchronized (gFrameRing) {
        if (gFrameRing.count == 0) return NULL;

        // tmp/highlights/{ts}_{tag}.mp4
        NSString *tmpDir = NSTemporaryDirectory();
        NSString *highlightsDir = [tmpDir stringByAppendingPathComponent:@"highlights"];
        [[NSFileManager defaultManager] createDirectoryAtPath:highlightsDir
                                  withIntermediateDirectories:YES
                                                   attributes:nil error:nil];

        NSString *safeTag = [gCurrentTag stringByReplacingOccurrencesOfString:@"/" withString:@"_"];
        NSString *filename = [NSString stringWithFormat:@"%lld_%@.mp4",
                              (long long)([[NSDate date] timeIntervalSince1970] * 1000),
                              safeTag];
        NSString *outPath = [highlightsDir stringByAppendingPathComponent:filename];
        NSURL *outUrl = [NSURL fileURLWithPath:outPath];
        [[NSFileManager defaultManager] removeItemAtURL:outUrl error:nil];

        // AVAssetWriter 설정 — H.264 720x1280
        NSError *err = nil;
        AVAssetWriter *writer = [[AVAssetWriter alloc] initWithURL:outUrl
                                                          fileType:AVFileTypeMPEG4
                                                             error:&err];
        if (err != nil) { NSLog(@"[ShortGeta] AVAssetWriter init: %@", err); return NULL; }

        NSDictionary *videoSettings = @{
            AVVideoCodecKey: AVVideoCodecTypeH264,
            AVVideoWidthKey: @720,
            AVVideoHeightKey: @1280,
        };
        AVAssetWriterInput *input = [[AVAssetWriterInput alloc] initWithMediaType:AVMediaTypeVideo
                                                                   outputSettings:videoSettings];
        input.expectsMediaDataInRealTime = NO;
        [writer addInput:input];
        [writer startWriting];
        [writer startSessionAtSourceTime:kCMTimeZero];

        // ring buffer 의 frame 들 append (timestamp 재정렬은 단순화)
        CMTime frameDuration = CMTimeMake(1, 30);
        CMTime currentTime = kCMTimeZero;
        for (NSValue *v in gFrameRing) {
            CMSampleBufferRef buf = (CMSampleBufferRef)[v pointerValue];
            // re-time
            CMItemCount count = 1;
            CMSampleTimingInfo timing = { frameDuration, currentTime, kCMTimeInvalid };
            CMSampleBufferRef retimedBuf = NULL;
            CMSampleBufferCreateCopyWithNewTiming(kCFAllocatorDefault, buf, count, &timing, &retimedBuf);
            if (retimedBuf != NULL && input.isReadyForMoreMediaData) {
                [input appendSampleBuffer:retimedBuf];
                CFRelease(retimedBuf);
            }
            CFRelease(buf);
            currentTime = CMTimeAdd(currentTime, frameDuration);
        }
        [gFrameRing removeAllObjects];

        [input markAsFinished];
        dispatch_semaphore_t sem = dispatch_semaphore_create(0);
        [writer finishWritingWithCompletionHandler:^{
            dispatch_semaphore_signal(sem);
        }];
        dispatch_semaphore_wait(sem, dispatch_time(DISPATCH_TIME_NOW, 5 * NSEC_PER_SEC));

        if (writer.status != AVAssetWriterStatusCompleted) {
            NSLog(@"[ShortGeta] writer status=%ld error=%@", (long)writer.status, writer.error);
            return NULL;
        }

        gLastClipPath = outPath;
        NSLog(@"[ShortGeta] flush → %@", gLastClipPath);
        return [gLastClipPath UTF8String];
    }
}

void _ShortGeta_ShareLastClip(void) {
    if (gLastClipPath == nil) {
        NSLog(@"[ShortGeta] ShareLastClip: no clip");
        return;
    }
    NSURL *url = [NSURL fileURLWithPath:gLastClipPath];
    if (![[NSFileManager defaultManager] fileExistsAtPath:gLastClipPath]) {
        NSLog(@"[ShortGeta] ShareLastClip: file not found %@", gLastClipPath);
        return;
    }

    UIViewController *root = nil;
    if (@available(iOS 13.0, *)) {
        for (UIScene *scene in [UIApplication sharedApplication].connectedScenes) {
            if ([scene isKindOfClass:[UIWindowScene class]]) {
                UIWindowScene *ws = (UIWindowScene *)scene;
                for (UIWindow *w in ws.windows) {
                    if (w.isKeyWindow) { root = w.rootViewController; break; }
                }
                if (root) break;
            }
        }
    }
    if (root == nil) {
        root = [UIApplication sharedApplication].keyWindow.rootViewController;
    }
    if (root == nil) {
        NSLog(@"[ShortGeta] ShareLastClip: no root view controller");
        return;
    }

    UIActivityViewController *vc = [[UIActivityViewController alloc]
        initWithActivityItems:@[url] applicationActivities:nil];

    // iPad popover anchor
    if (vc.popoverPresentationController != nil) {
        vc.popoverPresentationController.sourceView = root.view;
        vc.popoverPresentationController.sourceRect = CGRectMake(
            root.view.bounds.size.width / 2, root.view.bounds.size.height / 2, 0, 0);
        vc.popoverPresentationController.permittedArrowDirections = 0;
    }

    [root presentViewController:vc animated:YES completion:nil];
}

} // extern "C"
