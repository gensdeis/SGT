using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.SoccerSide
{
    // 1:1 축구 — 좌우 이동 + 탭=점프 로 공 차기. 60초.
    public class SoccerSideGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "soccer_side_v1";
        public string Title => "1:1 축구";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 60f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Daily };

        private const int MaxScore = 500;
        private const int GoalScore = 100;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;
        private float _playerX = 0.5f; // 0~1
        private float _ballX = 0.5f, _ballY = 0.5f;
        private float _ballVx, _ballVy;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private RectTransform _playerRt, _ballRt;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0); _running = true;
            _ballX = 0.5f; _ballY = 0.4f; _ballVx = 0.1f; _ballVy = 0f;
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
            if (!_running || input.Type != InputEventType.Down) return;
            // 탭 위치로 이동 + 공 근처면 킥
            _playerX = input.ScreenPosition.x / Screen.width;
            float dist = Mathf.Abs(_playerX - _ballX);
            if (dist < 0.15f && _ballY < 0.4f)
            {
                _ballVy = 0.5f; // 공 위로 킥
                _ballVx = (_playerX - _ballX) * -2f;
            }
        }

        private void Update()
        {
            if (!_running) return;
            float dt = Time.deltaTime;
            _ballX += _ballVx * dt;
            _ballY += _ballVy * dt;
            _ballVy -= 0.3f * dt; // 중력
            if (_ballY < 0.15f) { _ballY = 0.15f; _ballVy = Mathf.Abs(_ballVy) * 0.5f; }
            if (_ballX < 0.05f || _ballX > 0.95f) _ballVx = -_ballVx;

            // 골 판정 (상단 골대)
            if (_ballY > 0.85f && _ballX > 0.3f && _ballX < 0.7f)
            {
                _score = _score + GoalScore;
                _ballX = 0.5f; _ballY = 0.4f; _ballVx = 0.1f; _ballVy = 0f;
            }

            if (_playerRt != null) { _playerRt.anchorMin = new Vector2(_playerX - 0.05f, 0.12f); _playerRt.anchorMax = new Vector2(_playerX + 0.05f, 0.22f); }
            if (_ballRt != null) { _ballRt.anchorMin = new Vector2(_ballX - 0.03f, _ballY); _ballRt.anchorMax = new Vector2(_ballX + 0.03f, _ballY + 0.05f); }
            if (_label != null) _label.text = $"1:1 축구\n골: {_score.Value / GoalScore}\n\n탭해서 공 차기!";
        }

        private void BuildUI()
        {
            _root = new GameObject("SoccerSideUI"); _root.transform.SetParent(transform, false);
            var c = _root.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = _root.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280); s.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.4f, 0.7f, 0.9f); // 하늘

            // 플레이어
            var player = new GameObject("Player"); player.transform.SetParent(_root.transform, false);
            _playerRt = player.AddComponent<RectTransform>();
            _playerRt.offsetMin = Vector2.zero; _playerRt.offsetMax = Vector2.zero;
            player.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.9f);

            // 공
            var ball = new GameObject("Ball"); ball.transform.SetParent(_root.transform, false);
            _ballRt = ball.AddComponent<RectTransform>();
            _ballRt.offsetMin = Vector2.zero; _ballRt.offsetMax = Vector2.zero;
            var ballImg = ball.AddComponent<Image>();
            ballImg.color = Color.white;
            ballImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();

            var lg = new GameObject("Label"); lg.transform.SetParent(_root.transform, false);
            var lr = lg.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.05f, 0.88f); lr.anchorMax = new Vector2(0.95f, 0.98f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            _label = lg.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 36; _label.alignment = TextAlignmentOptions.Center; _label.color = Color.white;
        }
    }
}
