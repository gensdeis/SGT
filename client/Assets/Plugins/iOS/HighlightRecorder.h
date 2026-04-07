// 숏게타 iOS highlight recording — C 함수 헤더 (Unity P/Invoke)
//
// 구현: HighlightRecorder.mm (ReplayKit RPScreenRecorder 사용)
// 빌드: Unity → Build & Run iOS → Xcode 자동 import

#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

// 사용 가능 여부 (디바이스 지원 + 권한 등)
bool _ShortGeta_IsAvailable(void);

// 녹화 시작. tag 는 session 식별용.
void _ShortGeta_StartRecording(const char* tag);

// 녹화 종료.
void _ShortGeta_StopRecording(void);

// 직전 3초 클립을 임시 디렉토리에 MP4 로 저장하고 경로 반환.
// 호출자(C#)는 Marshal.PtrToStringAnsi 로 변환.
// 아무 클립도 없으면 NULL.
const char* _ShortGeta_FlushLastClipPath(void);

#ifdef __cplusplus
}
#endif
