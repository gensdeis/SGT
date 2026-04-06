using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.PokerFace
{
    // 포커페이스 유지 — "절대 탭하지 마" 카운트다운 게임.
    //
    // 매초 +14 자동 가산 (60s × 14 = 840 → max 800 클램프)
    // 5초마다 가짜 "보상받기" 버튼이 1초간 등장
    // 가짜 버튼 탭 시 -100 (포커페이스 깨짐)
    public class PokerFaceGame : MonoBehaviour, IMinigame
    {
        public string GameId => "poker_face_v1";
        public string Title => "포커페이스 유지";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 60f;
        public string[] Tags => new[] { GameTags.Awareness, GameTags.Internet, GameTags.Dark };

        private const int MaxScore = 800;
        private const int PerSecondGain = 14;
        private const int TempPenalty = -100;
        private const float TemptInterval = 5f;
        private const float TemptDuration = 1f;

        private SafeInt _score;
        private bool _running;
        private float _lastSecondTick;
        private float _lastTemptAt;
        private bool _temptVisible;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private GameObject _temptButton;

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            _lastSecondTick = Time.realtimeSinceStartup;
            _lastTemptAt = Time.realtimeSinceStartup;
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
            Debug.Log($"[PokerFace] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            // 전체 화면 탭은 무시. 가짜 버튼은 자체 onClick 사용.
        }

        private void Update()
        {
            if (!_running) return;
            float now = Time.realtimeSinceStartup;

            // 매초 자동 가산
            if (now - _lastSecondTick >= 1f)
            {
                _score = _score + PerSecondGain;
                _lastSecondTick = now;
                UpdateLabel();
            }

            // 유혹 버튼 등장/소멸
            if (!_temptVisible && now - _lastTemptAt >= TemptInterval)
            {
                ShowTempt();
            }
            if (_temptVisible && now - _lastTemptAt >= TemptInterval + TemptDuration)
            {
                HideTempt();
            }
        }

        private void OnTemptTapped()
        {
            _score = _score + TempPenalty;
            HideTempt();
            UpdateLabel();
        }

        private void ShowTempt()
        {
            _temptVisible = true;
            _lastTemptAt = Time.realtimeSinceStartup;
            if (_temptButton != null) _temptButton.SetActive(true);
        }

        private void HideTempt()
        {
            _temptVisible = false;
            _lastTemptAt = Time.realtimeSinceStartup;
            if (_temptButton != null) _temptButton.SetActive(false);
        }

        private void BuildUI()
        {
            _root = new GameObject("PokerFaceUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.1f, 0.5f);
            lrt.anchorMax = new Vector2(0.9f, 0.85f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 52;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            // 유혹 버튼
            _temptButton = new GameObject("TemptButton");
            _temptButton.transform.SetParent(_root.transform, false);
            var brt = _temptButton.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.15f, 0.15f);
            brt.anchorMax = new Vector2(0.85f, 0.35f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = _temptButton.AddComponent<Image>();
            img.color = new Color(1f, 0.3f, 0.3f);
            var btn = _temptButton.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnTemptTapped);

            var btnLabelGo = new GameObject("TemptLabel");
            btnLabelGo.transform.SetParent(_temptButton.transform, false);
            var trt = btnLabelGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var btnText = btnLabelGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "🎁 보상받기 🎁";
            btnText.fontSize = 56;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            _temptButton.SetActive(false);
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            _label.text = $"포커페이스 유지\n\n" +
                          $"절대 아무것도 탭하지 마라\n\n" +
                          $"점수: {_score.Value}";
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
