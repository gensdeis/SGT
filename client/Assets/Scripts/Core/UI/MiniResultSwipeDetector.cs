using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ShortGeta.Core.UI
{
    // 미니 결과 화면에서 위로 스와이프 감지 (쇼츠 UX).
    // 스와이프 중 화면이 위로 밀려나가는 드래그 + 릴리즈 시 전환 애니메이션.
    public class MiniResultSwipeDetector : MonoBehaviour
    {
        public System.Action OnSwipeUp;

        private const float SwipeThreshold = 80f;
        private const float AnimDuration = 0.25f;
        private Vector2 _touchStart;
        private bool _tracking;
        private bool _animating;
        private RectTransform _panelRt;

        private void Awake()
        {
            _panelRt = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (_animating) return;

            // 마우스
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _touchStart = UnityEngine.Input.mousePosition;
                _tracking = true;
            }
            if (_tracking && UnityEngine.Input.GetMouseButton(0) && _panelRt != null)
            {
                float dy = UnityEngine.Input.mousePosition.y - _touchStart.y;
                if (dy > 0) _panelRt.anchoredPosition = new Vector2(0, dy * 0.5f);
            }
            if (_tracking && UnityEngine.Input.GetMouseButtonUp(0))
            {
                _tracking = false;
                float dy = UnityEngine.Input.mousePosition.y - _touchStart.y;
                if (dy > SwipeThreshold) PlaySwipeOutAsync().Forget();
                else if (_panelRt != null) _panelRt.anchoredPosition = Vector2.zero;
            }

            // 터치
            if (UnityEngine.Input.touchCount > 0)
            {
                var touch = UnityEngine.Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _touchStart = touch.position;
                    _tracking = true;
                }
                else if (_tracking && touch.phase == TouchPhase.Moved && _panelRt != null)
                {
                    float dy = touch.position.y - _touchStart.y;
                    if (dy > 0) _panelRt.anchoredPosition = new Vector2(0, dy * 0.5f);
                }
                else if (_tracking && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                {
                    _tracking = false;
                    float dy = touch.position.y - _touchStart.y;
                    if (dy > SwipeThreshold) PlaySwipeOutAsync().Forget();
                    else if (_panelRt != null) _panelRt.anchoredPosition = Vector2.zero;
                }
            }
        }

        // 쇼츠 스타일: 현재 화면이 위로 슬라이드 아웃
        private async UniTaskVoid PlaySwipeOutAsync()
        {
            _animating = true;
            if (_panelRt == null) { OnSwipeUp?.Invoke(); return; }
            float start = _panelRt.anchoredPosition.y;
            float target = Screen.height;
            float t = 0f;
            while (t < AnimDuration)
            {
                if (_panelRt == null) break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / AnimDuration);
                k = 1f - (1f - k) * (1f - k); // easeOut
                _panelRt.anchoredPosition = new Vector2(0, Mathf.Lerp(start, target, k));
                await UniTask.Yield();
            }
            _animating = false;
            OnSwipeUp?.Invoke();
        }
    }
}
