using UnityEngine;

namespace ShortGeta.Core.UI
{
    // 미니 결과 화면에서 위로 스와이프 감지 (쇼츠 UX).
    // _miniResultPanel 에 AddComponent 로 부착. OnSwipeUp 콜백 발화.
    public class MiniResultSwipeDetector : MonoBehaviour
    {
        public System.Action OnSwipeUp;

        private const float SwipeThreshold = 80f; // px
        private Vector2 _touchStart;
        private bool _tracking;

        private void Update()
        {
            // 마우스 (Editor / PC)
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _touchStart = UnityEngine.Input.mousePosition;
                _tracking = true;
            }
            if (_tracking && UnityEngine.Input.GetMouseButtonUp(0))
            {
                _tracking = false;
                float dy = UnityEngine.Input.mousePosition.y - _touchStart.y;
                if (dy > SwipeThreshold)
                {
                    OnSwipeUp?.Invoke();
                }
            }

            // 터치 (모바일)
            if (UnityEngine.Input.touchCount > 0)
            {
                var touch = UnityEngine.Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    _touchStart = touch.position;
                    _tracking = true;
                }
                else if (_tracking && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
                {
                    _tracking = false;
                    float dy = touch.position.y - _touchStart.y;
                    if (dy > SwipeThreshold)
                    {
                        OnSwipeUp?.Invoke();
                    }
                }
            }
        }
    }
}
