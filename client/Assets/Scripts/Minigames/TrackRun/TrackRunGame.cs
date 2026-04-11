using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.TrackRun
{
    // 육상 게임 — 좌우 번갈아 탭 → 파워게이지 충전 → 거리.
    public class TrackRunGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "track_run_v1";
        public string Title => "육상 게임";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 20f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Everyday };

        private const int MaxScore = 1000;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;
        private float _power; // 0~1
        private bool _lastLeft; // 번갈아 탭 체크

        private GameObject _root;
        private TextMeshProUGUI _label;
        private Image _powerBar;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            _power = 0f;
            _lastLeft = false;
            BuildUI();
        }

        public void OnGameEnd()
        {
            _running = false;
            _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running || input.Type != InputEventType.Down) return;
            bool isLeft = input.ScreenPosition.x < Screen.width / 2f;
            if (isLeft != _lastLeft)
            {
                float gain = _difficulty == 1 ? 0.03f : (_difficulty == -1 ? 0.06f : 0.045f);
                _power = Mathf.Clamp01(_power + gain);
                _score = SafeInt.From(Mathf.RoundToInt(_power * MaxScore));
            }
            _lastLeft = isLeft;
            UpdateUI();
        }

        private void Update()
        {
            if (!_running) return;
            float decay = _difficulty == 1 ? 0.03f : 0.015f;
            _power = Mathf.Max(0, _power - decay * Time.deltaTime);
            _score = SafeInt.From(Mathf.RoundToInt(_power * MaxScore));
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_label != null) _label.text = $"육상 게임\n거리: {_score.Value}M\n\n좌↔우 번갈아 탭!";
            if (_powerBar != null) _powerBar.fillAmount = _power;
        }

        private void BuildUI()
        {
            _root = new GameObject("TrackRunUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.08f, 0.20f, 0.06f); // 잔디

            var labelGo = new GameObject("Label"); labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.55f); lrt.anchorMax = new Vector2(0.95f, 0.90f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 56; _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            // 파워 게이지 바
            var barBg = new GameObject("BarBg"); barBg.transform.SetParent(_root.transform, false);
            var brt = barBg.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.1f, 0.25f); brt.anchorMax = new Vector2(0.9f, 0.35f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            barBg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);

            var fill = new GameObject("Fill"); fill.transform.SetParent(barBg.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _powerBar = fill.AddComponent<Image>();
            _powerBar.color = new Color(1f, 0.7f, 0.0f); // 무지개→심플 주황
            _powerBar.type = Image.Type.Filled;
            _powerBar.fillMethod = Image.FillMethod.Horizontal;
            _powerBar.fillAmount = 0f;
            UpdateUI();
        }
    }
}
