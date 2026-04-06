using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.UI.Mobile
{
    // 비침습적 토스트. 팝업 금지 원칙 (BACKEND_PLAN.md §"Non-blocking 점수 제출 UX") 준수.
    // 정적 헬퍼로 호출: Toast.Show("점수 저장 실패");
    // 자동 생성 (런타임에 Canvas 만듦), 3초 후 사라짐.
    public class Toast : MonoBehaviour
    {
        private static Toast _instance;

        public static void Show(string message, float seconds = 3f)
        {
            EnsureInstance();
            _instance.ShowInternal(message, seconds);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("[ToastCanvas]");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(720, 1280);
            go.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.1f);
            rt.anchorMax = new Vector2(0.9f, 0.16f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panel.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20, 10);
            trt.offsetMax = new Vector2(-20, -10);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.fontSize = 36;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.text = "";

            _instance = panel.AddComponent<Toast>();
            _instance._text = txt;
            _instance._panel = panel;
            panel.SetActive(false);
        }

        private TextMeshProUGUI _text;
        private GameObject _panel;
        private float _hideAt;

        private void ShowInternal(string msg, float sec)
        {
            _text.text = msg;
            _panel.SetActive(true);
            _hideAt = Time.realtimeSinceStartup + sec;
        }

        private void Update()
        {
            if (_panel.activeSelf && Time.realtimeSinceStartup > _hideAt)
            {
                _panel.SetActive(false);
            }
        }
    }
}
