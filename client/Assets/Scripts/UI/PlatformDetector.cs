using UnityEngine;

namespace ShortGeta.UI
{
    // 모바일 vs PC UI 분기 결정.
    //
    // 우선순위:
    //   1. forceLayout (Editor 토글) 가 None 이 아니면 그대로 사용
    //   2. Application.isMobilePlatform → Mobile
    //   3. Screen 비율이 세로형 (h > w) → Mobile
    //   4. 그 외 → PC
    //
    // 후속: 윈도우 리사이즈 시 동적 전환 — 본 iter 는 부팅 시 1회 결정.
    public enum LayoutMode { Mobile, PC }

    public enum LayoutOverride { None, ForceMobile, ForcePC }

    public static class PlatformDetector
    {
        public static LayoutOverride EditorOverride = LayoutOverride.None;

        public static LayoutMode Detect()
        {
            switch (EditorOverride)
            {
                case LayoutOverride.ForceMobile: return LayoutMode.Mobile;
                case LayoutOverride.ForcePC:     return LayoutMode.PC;
            }
#if UNITY_EDITOR
            // Editor 기본은 Mobile (Android 타겟). PC 보고싶으면 EditorOverride 사용.
            return LayoutMode.Mobile;
#elif UNITY_ANDROID || UNITY_IOS
            return LayoutMode.Mobile;
#else
            if (Application.isMobilePlatform) return LayoutMode.Mobile;
            if (Screen.height > Screen.width) return LayoutMode.Mobile;
            return LayoutMode.PC;
#endif
        }
    }
}
