using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.SoccerTopdown
{
    // 탑뷰 축구 — 공을 골대 방향으로 스와이프. 5번 슛 기회.
    public class SoccerTopdownGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "soccer_topdown_v1";
        public string Title => "탑뷰 축구";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Daily };

        private const int MaxScore = 500;
        private const int GoalScore = 100;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;
        private int _shots;
        private const int MaxShots = 5;
        private Vector2 _swipeStart;
        private bool _swiping;

        private GameObject _root;
        private TextMeshProUGUI _label;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0); _running = true; _shots = 0;
            BuildUI();
        }

        public void OnGameEnd()
        {
            _running = false; _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running || _shots >= MaxShots) return;
            if (input.Type == InputEventType.Down)
            {
                _swipeStart = input.ScreenPosition;
                _swiping = true;
            }
            else if (input.Type == InputEventType.Up && _swiping)
            {
                _swiping = false;
                float dy = input.ScreenPosition.y - _swipeStart.y;
                if (dy > 60f) // 위로 스와이프 = 슛
                {
                    _shots++;
                    float accuracy = Mathf.Abs(input.ScreenPosition.x - Screen.width / 2f) / (Screen.width / 2f);
                    float threshold = _difficulty == 1 ? 0.25f : (_difficulty == -1 ? 0.55f : 0.4f);
                    if (accuracy < threshold)
                    {
                        _score = _score + GoalScore;
                        UpdateLabel("골!!!");
                    }
                    else
                    {
                        UpdateLabel("빗나감...");
                    }
                }
            }
        }

        private void UpdateLabel(string status)
        {
            if (_label != null)
                _label.text = $"탑뷰 축구\n점수: {_score.Value}\n남은 슛: {MaxShots - _shots}\n\n{status}\n\n위로 스와이프 = 슛!";
        }

        private void BuildUI()
        {
            _root = new GameObject("SoccerTopUI"); _root.transform.SetParent(transform, false);
            var c = _root.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = _root.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280); s.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.30f, 0.10f); // 잔디
            var bgSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg(GameId);
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }

            var lg = new GameObject("Label"); lg.transform.SetParent(_root.transform, false);
            var lr = lg.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.05f, 0.3f); lr.anchorMax = new Vector2(0.95f, 0.85f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            _label = lg.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 52; _label.alignment = TextAlignmentOptions.Center; _label.color = Color.white;
            UpdateLabel("준비!");
        }
    }
}
