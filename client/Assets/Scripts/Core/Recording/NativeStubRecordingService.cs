using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // Android / iOS 용 placeholder. 실제 native plugin 연동은 Iter 2B' 에서.
    // 모든 호출 no-op 또는 warning log.
    public class NativeStubRecordingService : MonoBehaviour, IRecordingService
    {
        public bool IsSupported => false;
        public bool IsRecording => false;

        public void StartRecording(string sessionTag)
        {
            Debug.Log($"[Recording] (stub) StartRecording {sessionTag} — Iter 2B' 에서 native 구현 예정");
        }

        public void StopRecording()
        {
            // no-op
        }

        public SavedHighlight? FlushLastClip()
        {
            return null;
        }

        public void OpenLastClipExternally()
        {
            Debug.Log("[Recording] (stub) OpenLastClipExternally — Android/iOS native share 는 Iter 2B'");
        }
    }
}
