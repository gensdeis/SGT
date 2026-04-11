using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.FlyCatch
{
    // 파리 잡기 — 랜덤 위치로 이동하는 파리 탭. 20초.
    public class FlyCatchGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "fly_catch_v1";
        public string Title => "파리 잡기";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 20f;
        public string[] Tags => new[] { GameTags.Reaction, GameTags.Animal };

        private const int MaxScore = 800;
        private const int CatchGain = 20;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private RectTransform _flyRt;
        private float _nextMove;
        private float _moveInterval = 1.2f;

        public void SetDifficulty(int i)
        {
            _difficulty = Mathf.Clamp(i, -1, 1);
            _moveInterval = _difficulty == 1 ? 0.7f : (_difficulty == -1 ? 1.8f : 1.2f);
        }

        public void OnGameStart()
        {
            _score = SafeInt.From(0); _running = true;
            _nextMove = Time.realtimeSinceStartup + _moveInterval;
            BuildUI();
        }

        public void OnGameEnd()
        {
            _running = false;
            _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => _score.Value;
        public void OnInput(InputEvent input) { /* 버튼 onClick 으로 처리 */ }

        private void OnFlyTapped()
        {
            if (!_running) return;
            _score = _score + CatchGain;
            if (_score.Value > MaxScore) _score.Value = MaxScore;
            MoveFly();
            UpdateLabel();
        }

        private void Update()
        {
            if (!_running) return;
            if (Time.realtimeSinceStartup >= _nextMove)
            {
                MoveFly();
                _nextMove = Time.realtimeSinceStartup + _moveInterval;
            }
        }

        private void MoveFly()
        {
            if (_flyRt == null) return;
            float x = Random.Range(0.1f, 0.8f);
            float y = Random.Range(0.15f, 0.75f);
            _flyRt.anchorMin = new Vector2(x, y);
            _flyRt.anchorMax = new Vector2(x + 0.15f, y + 0.12f);
        }

        private void UpdateLabel()
        {
            if (_label != null) _label.text = $"파리 잡기!\n점수: {_score.Value}";
        }

        private void BuildUI()
        {
            _root = new GameObject("FlyCatchUI"); _root.transform.SetParent(transform, false);
            var c = _root.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 100;
            var s = _root.AddComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(720, 1280); s.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Bg"); bg.transform.SetParent(_root.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.75f, 0.68f, 0.50f); // 모래 배경

            var lg = new GameObject("Label"); lg.transform.SetParent(_root.transform, false);
            var lr = lg.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0.05f, 0.85f); lr.anchorMax = new Vector2(0.95f, 0.97f);
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            _label = lg.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 44; _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.2f, 0.15f, 0.1f);
            UpdateLabel();

            // 파리 (터치 대상)
            var fly = new GameObject("Fly"); fly.transform.SetParent(_root.transform, false);
            _flyRt = fly.AddComponent<RectTransform>();
            _flyRt.offsetMin = Vector2.zero; _flyRt.offsetMax = Vector2.zero;
            var flyImg = fly.AddComponent<Image>();
            flyImg.color = new Color(0.15f, 0.15f, 0.15f);
            flyImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();
            var btn = fly.AddComponent<Button>();
            btn.targetGraphic = flyImg;
            btn.onClick.AddListener(OnFlyTapped);

            var flyLabel = new GameObject("FlyEmoji"); flyLabel.transform.SetParent(fly.transform, false);
            var flrt = flyLabel.AddComponent<RectTransform>();
            flrt.anchorMin = Vector2.zero; flrt.anchorMax = Vector2.one;
            flrt.offsetMin = Vector2.zero; flrt.offsetMax = Vector2.zero;
            var ft = flyLabel.AddComponent<TextMeshProUGUI>();
            ft.text = "🪰"; ft.fontSize = 80; ft.alignment = TextAlignmentOptions.Center;

            MoveFly();
        }
    }
}
