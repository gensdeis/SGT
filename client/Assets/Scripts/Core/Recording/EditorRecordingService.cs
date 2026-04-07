using System.Collections;
using UnityEngine;

namespace ShortGeta.Core.Recording
{
    // Unity Editor / Standalone 용 녹화 서비스.
    // 100ms 마다 ScreenCapture.CaptureScreenshotAsTexture() 로 캡처.
    // 30 프레임 (3초) circular buffer 유지.
    // StopRecording 시 buffer snapshot → HighlightStorage 로 PNG sequence 저장.
    //
    // 모바일 빌드 시 사용 금지 — NativeStubRecordingService 사용.
    public class EditorRecordingService : MonoBehaviour, IRecordingService
    {
        private const int FrameRate = 10;       // 10 fps
        private const int BufferSec = 3;        // 3 초
        private const int BufferSize = FrameRate * BufferSec; // 30 frames
        private const float CaptureInterval = 1f / FrameRate; // 0.1s

        private RecordingFrameBuffer _buffer;
        private HighlightStorage _storage;
        private string _currentTag;
        private bool _recording;
        private SavedHighlight? _lastClip;
        private Coroutine _captureCoroutine;

        public bool IsSupported => true;
        public bool IsRecording => _recording;

        private void Awake()
        {
            _buffer = new RecordingFrameBuffer(BufferSize);
            _storage = new HighlightStorage(System.IO.Path.Combine(Application.persistentDataPath, "highlights"));
            Debug.Log($"[Recording] EditorRecordingService ready, baseDir={_storage.BaseDir}");
        }

        private void OnDestroy()
        {
            _buffer?.Clear();
        }

        public void StartRecording(string sessionTag)
        {
            if (_recording)
            {
                Debug.LogWarning("[Recording] already recording, ignoring StartRecording");
                return;
            }
            _currentTag = sessionTag;
            _buffer.Clear();
            _recording = true;
            _captureCoroutine = StartCoroutine(CaptureLoop());
            Debug.Log($"[Recording] start session={sessionTag}");
        }

        public void StopRecording()
        {
            if (!_recording) return;
            _recording = false;
            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }
            Debug.Log($"[Recording] stop session={_currentTag} captured={_buffer.Count}");
        }

        public SavedHighlight? FlushLastClip()
        {
            if (_buffer.Count == 0)
            {
                Debug.LogWarning("[Recording] FlushLastClip but buffer is empty");
                return null;
            }
            var frames = _buffer.Snapshot();
            string dir = _storage.CreateSessionDir(_currentTag ?? "untagged");
            _storage.SavePngSequence(dir, frames);
            var clip = new SavedHighlight
            {
                Path = dir,
                Format = "png-sequence",
                FrameCount = frames.Count,
                DurationSec = (float)frames.Count / FrameRate,
                CreatedAt = System.DateTime.UtcNow,
                SessionTag = _currentTag,
            };
            _lastClip = clip;
            _buffer.Clear();
            Debug.Log($"[Recording] saved {frames.Count} frames → {dir}");
            return clip;
        }

        public void OpenLastClipExternally()
        {
            if (!_lastClip.HasValue)
            {
                Debug.LogWarning("[Recording] OpenLastClipExternally but no clip available");
                return;
            }
            string url = "file://" + _lastClip.Value.Path.Replace("\\", "/");
            Application.OpenURL(url);
            Debug.Log($"[Recording] opened {url}");
        }

        public void ShareLastClip()
        {
            // Editor 에서는 share sheet 가 없으므로 폴더 열기와 동일.
            OpenLastClipExternally();
        }

        private IEnumerator CaptureLoop()
        {
            // 첫 capture 전 살짝 대기 (씬 안정화)
            yield return new WaitForSecondsRealtime(CaptureInterval);
            while (_recording)
            {
                CaptureFrame();
                yield return new WaitForSecondsRealtime(CaptureInterval);
            }
        }

        private void CaptureFrame()
        {
            // ScreenCapture.CaptureScreenshotAsTexture 는 메인 스레드 stall 가능.
            // 작은 캡처 (Editor) 한정이라 OK. 모바일에서는 NativeStub 사용.
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                // 워터마크 (Iter 2B') — 우하단 박스 + W 마크 placeholder
                WatermarkOverlay.ApplyInPlace(tex);
                _buffer.Push(tex);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Recording] capture frame failed: {e.Message}");
            }
        }
    }
}
