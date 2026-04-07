using UnityEngine;

namespace ShortGeta.Core.UI
{
    // shortgeta-plan-v1.3.html 에서 추출한 디자인 토큰.
    // 모든 신규 UI 는 여기 상수만 참조한다 — 직접 hex/픽셀 값 금지.
    public static class DesignTokens
    {
        // ─── Colors ───
        public static readonly Color Bg          = Hex("#0c0e13");
        public static readonly Color Surface     = Hex("#14161d");
        public static readonly Color Surface2    = Hex("#1b1e28");
        public static readonly Color Border      = Hex("#2a2e3a");
        public static readonly Color Text        = Hex("#e2e4ea");
        public static readonly Color TextDim     = Hex("#8b8fa3");
        public static readonly Color Accent      = Hex("#5eead4"); // teal
        public static readonly Color AccentDark  = Hex("#0F6E56");
        public static readonly Color PrimaryCTA  = Hex("#9FE1CB");
        public static readonly Color OnPrimary   = Hex("#04342C");
        public static readonly Color NavBg       = Hex("#111318");
        public static readonly Color QuickBg     = Hex("#085041");
        public static readonly Color Gold        = Hex("#fbbf24");

        // ─── Sizes (px in 720x1280 reference) ───
        public const float RadiusCard = 24f;   // 시각적 padding 단위로만 사용
        public const float RadiusPill = 36f;
        public const float RadiusBtn  = 28f;

        public const int FontTitle    = 64;
        public const int FontH2       = 44;
        public const int FontBody     = 28;
        public const int FontCaption  = 22;
        public const int FontTag      = 18;

        // ─── Helpers ───
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
