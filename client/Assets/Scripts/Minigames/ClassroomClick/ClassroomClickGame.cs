using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.ClassroomClick
{
    // 교실 클릭 게임 — 화면 전체 빠르게 탭, 15초 내 최다 클릭.
    public class ClassroomClickGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "classroom_click_v1";
        public string Title => "교실 클릭 게임";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 15f;
        public string[] Tags => new[] { GameTags.Reaction, GameTags.Everyday };

        private const int MaxScore = 500;
        private SafeInt _score;
        private bool _running;
        private int _difficulty;

        private GameObject _root;
        private TextMeshProUGUI _label;

        public void SetDifficulty(int i) => _difficulty = Mathf.Clamp(i, -1, 1);

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            BuildUI();
        }

        public void OnGameEnd()
        {
            _running = false;
            if (_score.Value > MaxScore) _score.Value = MaxScore;
            if (_score.Value < 0) _score.Value = 0;
            if (_root != null) Destroy(_root);
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running || input.Type != InputEventType.Down) return;
            int gain = _difficulty == 1 ? 1 : (_difficulty == -1 ? 3 : 2);
            _score = _score + gain;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (_label != null)
                _label.text = $"교실 클릭!\n\n{_score.Value}\n\n빠르게 탭!";
        }

        private void BuildUI()
        {
            _root = new GameObject("ClassroomClickUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bgGo.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.3f);
            lrt.anchorMax = new Vector2(0.95f, 0.85f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 72;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.9f, 0.95f, 1f);
            UpdateLabel();
        }
    }
}
