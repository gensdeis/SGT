using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Core.UI
{
    // 런타임 UI 생성 헬퍼. prefab 없이 코드로 패널/버튼/라벨을 만든다.
    // BootstrapController 같은 곳에서 ShowHome() 등이 사용.
    //
    // 라운드 처리는 RoundedSpriteFactory 의 procedural 9-slice sprite 사용.
    public static class UIBuilder
    {
        public static GameObject Panel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        // 라운드 사각 패널. radius 픽셀 단위.
        public static GameObject RoundedPanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color, int radius)
        {
            var go = Panel(parent, name, anchorMin, anchorMax, color);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSpriteFactory.GetRounded(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            return go;
        }

        // 원형 패널 (프로필 아바타 등).
        public static GameObject CirclePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = Panel(parent, name, anchorMin, anchorMax, color);
            var img = go.GetComponent<Image>();
            img.sprite = RoundedSpriteFactory.GetCircle();
            img.type = Image.Type.Simple;
            return go;
        }

        public static GameObject FillPanel(Transform parent, string name, Color color)
        {
            return Panel(parent, name, Vector2.zero, Vector2.one, color);
        }

        public static TextMeshProUGUI Label(Transform parent, string text, int fontSize,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
            Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin ?? Vector2.zero;
            rt.anchorMax = anchorMax ?? Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(Transform parent, string name, Color bg, Color fg,
            string label, int fontSize, Vector2 anchorMin, Vector2 anchorMax, Action onClick,
            int radius = 0)
        {
            GameObject go = radius > 0
                ? RoundedPanel(parent, name, anchorMin, anchorMax, bg, radius)
                : Panel(parent, name, anchorMin, anchorMax, bg);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var lt = Label(go.transform, label, fontSize, fg, TextAlignmentOptions.Center);
            lt.fontStyle = FontStyles.Bold;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // 라운드 태그 pill.
        public static GameObject Tag(Transform parent, string text)
        {
            var go = new GameObject("Tag");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40;
            le.preferredWidth = -1;
            var img = go.AddComponent<Image>();
            img.color = DesignTokens.Surface2;
            img.sprite = RoundedSpriteFactory.GetRounded(12);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(14, 14, 4, 4);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            var lblGo = new GameObject("Text");
            lblGo.transform.SetParent(go.transform, false);
            var lt = lblGo.AddComponent<TextMeshProUGUI>();
            lt.text = text;
            lt.fontSize = DesignTokens.FontTag;
            lt.color = DesignTokens.TextDim;
            lt.alignment = TextAlignmentOptions.Center;
            var fitter = lblGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }
    }
}
