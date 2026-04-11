using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.KakaoUnread
{
    // 카톡 읽씹하기 — 알림을 무시하는 게임.
    //
    // 매 1초마다 +30 자동 가산 (20s × 30 = 600)
    // 매 3초마다 가짜 카톡 버블이 1초간 화면 등장. 탭하면 -50 (읽씹 실패)
    public class KakaoUnreadGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "kakao_unread_v1";
        public string Title => "카톡 읽씹하기";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 20f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Daily };

        private const int MaxScore = 600;
        private const int PerSecondGain = 30;
        private const int ReadPenalty = -50;
        private float _notifInterval = 3f; // DDA: -1=4, 0=3, +1=2
        private const float NotifDuration = 1f;
        private int _difficulty;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
            _notifInterval = _difficulty == -1 ? 4f : (_difficulty == 1 ? 2f : 3f);
        }

        private SafeInt _score;
        private bool _running;
        private float _lastSecondTick;
        private float _lastNotifAt;
        private bool _notifVisible;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private GameObject _notifBubble;

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            _lastSecondTick = Time.realtimeSinceStartup;
            _lastNotifAt = Time.realtimeSinceStartup;
            BuildUI();
            UpdateLabel();
        }

        public void OnGameEnd()
        {
            _running = false;
            int v = _score.Value;
            if (v < 0) v = 0;
            if (v > MaxScore) v = MaxScore;
            _score.Value = v;
            if (_root != null) Destroy(_root);
            Debug.Log($"[KakaoUnread] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input) { }

        private void Update()
        {
            if (!_running) return;
            float now = Time.realtimeSinceStartup;
            if (now - _lastSecondTick >= 1f)
            {
                _score = _score + PerSecondGain;
                _lastSecondTick = now;
                UpdateLabel();
            }
            if (!_notifVisible && now - _lastNotifAt >= _notifInterval)
            {
                ShowNotif();
            }
            if (_notifVisible && now - _lastNotifAt >= _notifInterval + NotifDuration)
            {
                HideNotif();
            }
        }

        private void OnNotifTapped()
        {
            _score = _score + ReadPenalty;
            HideNotif();
            UpdateLabel();
        }

        private void ShowNotif()
        {
            _notifVisible = true;
            _lastNotifAt = Time.realtimeSinceStartup;
            if (_notifBubble != null) _notifBubble.SetActive(true);
        }

        private void HideNotif()
        {
            _notifVisible = false;
            _lastNotifAt = Time.realtimeSinceStartup;
            if (_notifBubble != null) _notifBubble.SetActive(false);
        }

        private void BuildUI()
        {
            _root = new GameObject("KakaoUnreadUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 채팅 배경
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.10f, 0.14f); // 다크 블루그레이 (채팅 앱 배경)

            // 안내 + 점수 (상단)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.55f);
            lrt.anchorMax = new Vector2(0.95f, 0.90f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 52;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.9f, 0.9f, 0.95f);

            // 가짜 알림 버블 — 카톡 노란색 + 라운드
            _notifBubble = new GameObject("NotifBubble");
            _notifBubble.transform.SetParent(_root.transform, false);
            var nrt = _notifBubble.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.08f, 0.20f);
            nrt.anchorMax = new Vector2(0.92f, 0.35f);
            nrt.offsetMin = Vector2.zero;
            nrt.offsetMax = Vector2.zero;
            var img = _notifBubble.AddComponent<Image>();
            img.color = new Color(0.99f, 0.90f, 0.0f); // 카카오 노랑
            img.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetRounded(20);
            img.type = Image.Type.Sliced;
            var btn = _notifBubble.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnNotifTapped);

            var nLabelGo = new GameObject("NotifLabel");
            nLabelGo.transform.SetParent(_notifBubble.transform, false);
            var trt = nLabelGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(20, 10);
            trt.offsetMax = new Vector2(-20, -10);
            var btnText = nLabelGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "💬 잠깐 시간 돼?";
            btnText.fontSize = 44;
            btnText.alignment = TextAlignmentOptions.Left;
            btnText.color = new Color(0.15f, 0.15f, 0.15f);

            _notifBubble.SetActive(false);
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            _label.text = $"카톡 읽씹하기\n\n알림을 절대 누르지 마라\n\n점수: {_score.Value}";
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
