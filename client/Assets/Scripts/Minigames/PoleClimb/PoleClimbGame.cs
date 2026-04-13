using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.PoleClimb
{
    // 장대 오르기 — 탭하면 위로, 안 하면 미끄러짐. 높이=점수.
    public class PoleClimbGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "pole_climb_v1";
        public string Title => "장대 오르기";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Daily };

        private const int MaxScore = 500;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;
        private float _height; // 0~1

        private GameObject _root;
        private TextMeshProUGUI _label;
        private Image _heightBar;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0); _running = true; _height = 0f;
            BuildUI();
        }

        public void OnGameEnd()
        {
            _running = false;
            _score.Value = Mathf.Clamp(Mathf.RoundToInt(_height * MaxScore), 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => Mathf.RoundToInt(_height * MaxScore);

        public void OnInput(InputEvent input)
        {
            if (!_running || input.Type != InputEventType.Down) return;
            float climb = _difficulty == 1 ? 0.025f : (_difficulty == -1 ? 0.05f : 0.035f);
            _height = Mathf.Clamp01(_height + climb);
        }

        private void Update()
        {
            if (!_running) return;
            float slide = _difficulty == 1 ? 0.025f : 0.015f;
            _height = Mathf.Max(0f, _height - slide * Time.deltaTime);
            _score = SafeInt.From(Mathf.RoundToInt(_height * MaxScore));
            if (_label != null) _label.text = $"장대 오르기\n높이: {_score.Value}m\n\n빠르게 탭하면 올라감!";
            if (_heightBar != null) _heightBar.fillAmount = _height;
        }

        private void BuildUI()
        {
            _root = new GameObject("PoleClimbUI"); _root.transform.SetParent(transform, false);
            var c = _root.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = _root.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280); s.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.45f, 0.65f, 0.85f); // 하늘
            var bgSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg(GameId);
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }

            var lg = new GameObject("Label"); lg.transform.SetParent(_root.transform, false);
            var lr = lg.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.05f, 0.6f); lr.anchorMax = new Vector2(0.95f, 0.90f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            _label = lg.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 48; _label.alignment = TextAlignmentOptions.Center; _label.color = Color.white;

            // 세로 높이 바 (좌측)
            var barBg = new GameObject("BarBg"); barBg.transform.SetParent(_root.transform, false);
            var brt = barBg.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.05f, 0.10f); brt.anchorMax = new Vector2(0.15f, 0.55f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            barBg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

            var fill = new GameObject("Fill"); fill.transform.SetParent(barBg.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _heightBar = fill.AddComponent<Image>();
            _heightBar.color = new Color(0.2f, 0.8f, 0.3f);
            _heightBar.type = Image.Type.Filled;
            _heightBar.fillMethod = Image.FillMethod.Vertical;
            _heightBar.fillAmount = 0f;
        }
    }
}
