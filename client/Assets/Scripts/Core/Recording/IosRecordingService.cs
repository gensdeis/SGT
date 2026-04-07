using System.Runtime.InteropServices;
using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // iOS ReplayKit (RPScreenRecorder) 호출 bridge.
    // Native source: client/Assets/Plugins/iOS/HighlightRecorder.{h,mm}
    //
    // Xcode 빌드 시 자동 link. Capabilities 에 ReplayKit 자동 추가는 보장 안 됨 →
    // client/docs/recording-native-build.md 의 iOS 절차 참조.
    public class IosRecordingService : MonoBehaviour, IRecordingService
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _ShortGeta_StartRecording(string tag);

        [DllImport("__Internal")]
        private static extern void _ShortGeta_StopRecording();

        [DllImport("__Internal")]
        private static extern System.IntPtr _ShortGeta_FlushLastClipPath();

        [DllImport("__Internal")]
        private static extern bool _ShortGeta_IsAvailable();
#endif

        private bool _recording;
        private SavedHighlight? _lastClip;

        public bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try { return _ShortGeta_IsAvailable(); }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        public bool IsRecording => _recording;

        public void StartRecording(string sessionTag)
        {
            if (_recording) return;
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                _ShortGeta_StartRecording(sessionTag ?? "untagged");
                _recording = true;
                Debug.Log($"[Recording] iOS start session={sessionTag}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] iOS start failed: {e.Message}");
            }
#endif
        }

        public void StopRecording()
        {
            if (!_recording) return;
#if UNITY_IOS && !UNITY_EDITOR
            try { _ShortGeta_StopRecording(); }
            catch (System.Exception e) { Debug.LogWarning($"[Recording] iOS stop failed: {e.Message}"); }
#endif
            _recording = false;
        }

        public SavedHighlight? FlushLastClip()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                var ptr = _ShortGeta_FlushLastClipPath();
                if (ptr == System.IntPtr.Zero) return null;
                string path = Marshal.PtrToStringAnsi(ptr);
                if (string.IsNullOrEmpty(path)) return null;
                _lastClip = new SavedHighlight
                {
                    Path = path,
                    Format = "mp4",
                    FrameCount = 90, // ReplayKit 30fps × 3s
                    DurationSec = 3f,
                    CreatedAt = System.DateTime.UtcNow,
                    SessionTag = "ios",
                };
                Debug.Log($"[Recording] iOS flush → {path}");
                return _lastClip;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] iOS flush failed: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public void OpenLastClipExternally()
        {
            if (!_lastClip.HasValue) return;
            Debug.Log($"[Recording] iOS share — Iter 2B''': open {_lastClip.Value.Path}");
        }
    }
}
