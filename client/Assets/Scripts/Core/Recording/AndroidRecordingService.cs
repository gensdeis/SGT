using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // Android 측 native plugin (com.shortgeta.recording.HighlightRecorder) 호출 bridge.
    // Java 소스: client/Assets/Plugins/Android/HighlightRecording/.../HighlightRecorder.java
    //
    // ⚠️ AAR 빌드 필요 — client/docs/recording-native-build.md 참조.
    // AAR 미설치 시 AndroidJavaClass 호출이 throws → catch 후 stub 처럼 동작.
    public class AndroidRecordingService : MonoBehaviour, IRecordingService
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _recorder;
        private bool _ready;
#endif
        private bool _recording;
        private SavedHighlight? _lastClip;

        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return _ready;
#else
                return false;
#endif
            }
        }

        public bool IsRecording => _recording;

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
                _recorder = new AndroidJavaObject("com.shortgeta.recording.HighlightRecorder", activity);
                _ready = true;
                Debug.Log("[Recording] AndroidRecordingService ready");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] AndroidRecordingService init failed (AAR not installed?): {e.Message}");
                _ready = false;
            }
#endif
        }

        public void StartRecording(string sessionTag)
        {
            if (_recording) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_ready) return;
            try
            {
                _recorder.Call("startRecording", sessionTag, 3);
                _recording = true;
                Debug.Log($"[Recording] Android start session={sessionTag}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] Android startRecording failed: {e.Message}");
            }
#endif
        }

        public void StopRecording()
        {
            if (!_recording) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _recorder.Call("stopRecording");
                _recording = false;
                Debug.Log("[Recording] Android stop");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] Android stopRecording failed: {e.Message}");
            }
#else
            _recording = false;
#endif
        }

        public SavedHighlight? FlushLastClip()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_ready) return null;
            try
            {
                string path = _recorder.Call<string>("flushLastClipPath");
                if (string.IsNullOrEmpty(path)) return null;
                _lastClip = new SavedHighlight
                {
                    Path = path,
                    Format = "jpeg-sequence",
                    FrameCount = 30,
                    DurationSec = 3f,
                    CreatedAt = System.DateTime.UtcNow,
                    SessionTag = "android",
                };
                Debug.Log($"[Recording] Android flush → {path}");
                return _lastClip;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] Android flush failed: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public void OpenLastClipExternally()
        {
            if (!_lastClip.HasValue) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _recorder.Call("openLastClip"); }
            catch (System.Exception e) { Debug.LogWarning($"[Recording] openLastClip failed: {e.Message}"); }
#endif
        }

        public void ShareLastClip()
        {
            if (!_lastClip.HasValue) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _recorder.Call("shareLastClip"); Debug.Log("[Recording] Android share triggered"); }
            catch (System.Exception e) { Debug.LogWarning($"[Recording] shareLastClip failed: {e.Message}"); }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_recorder != null) _recorder.Dispose();
#endif
        }
    }
}
