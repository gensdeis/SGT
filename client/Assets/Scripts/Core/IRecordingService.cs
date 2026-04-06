using System;

namespace ShortGeta.Core
{
    // 9:16 하이라이트 녹화 서비스 추상화.
    // 플랫폼별 구현체:
    //   - Editor / Standalone : EditorRecordingService (RenderTexture circular buffer + PNG sequence)
    //   - Android / iOS : NativeStubRecordingService (Iter 2B' 에서 native plugin 으로 교체 예정)
    //
    // 사용 흐름:
    //   1. 게임 시작 직전: StartRecording(tag)
    //   2. 게임 실행 중: 백그라운드에서 자동 캡처 (구현체 책임)
    //   3. 게임 종료 시: StopRecording()
    //   4. 결과 회수: FlushLastClip() → SavedHighlight 반환
    //   5. 사용자 검토: OpenLastClipExternally()
    public interface IRecordingService
    {
        bool IsSupported { get; }
        bool IsRecording { get; }

        void StartRecording(string sessionTag);
        void StopRecording();
        SavedHighlight? FlushLastClip();
        void OpenLastClipExternally();
    }

    public struct SavedHighlight
    {
        public string Path;        // 저장 경로 (디렉토리 또는 파일)
        public string Format;      // "png-sequence" / "mp4" / "none"
        public int FrameCount;
        public float DurationSec;
        public DateTime CreatedAt;
        public string SessionTag;  // 식별용 (예: "frog_catch_v1-1700000000")
    }
}
