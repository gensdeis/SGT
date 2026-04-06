using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.MathGenius
{
    // 수학 천재 도전 — 1자리수 +/- 4지선다.
    //
    // 정답 +50, 오답 -10. 30초 동안 30문제 정답 = 1500 (max).
    public class MathGeniusGame : MonoBehaviour, IMinigame
    {
        public string GameId => "math_genius_v1";
        public string Title => "수학 천재 도전";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Focus, GameTags.Bmovie };

        private const int MaxScore = 1500;
        private const int CorrectGain = 50;
        private const int WrongPenalty = -10;

        private SafeInt _score;
        private bool _running;
        private int _correctAnswer;

        private GameObject _root;
        private TextMeshProUGUI _question;
        private TextMeshProUGUI _scoreLabel;
        private Button[] _choices = new Button[4];
        private TextMeshProUGUI[] _choiceLabels = new TextMeshProUGUI[4];

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            BuildUI();
            NextQuestion();
        }

        public void OnGameEnd()
        {
            _running = false;
            int v = _score.Value;
            if (v < 0) v = 0;
            if (v > MaxScore) v = MaxScore;
            _score.Value = v;
            if (_root != null) Destroy(_root);
            Debug.Log($"[MathGenius] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input) { }

        private void NextQuestion()
        {
            if (!_running) return;
            int a = Random.Range(1, 10);
            int b = Random.Range(1, 10);
            bool isPlus = Random.Range(0, 2) == 0;
            string op = isPlus ? "+" : "-";
            int answer = isPlus ? a + b : a - b;
            _correctAnswer = answer;

            if (_question != null) _question.text = $"{a} {op} {b} = ?";

            // 4지선다 (정답 1개 + 오답 3개)
            int correctIdx = Random.Range(0, 4);
            for (int i = 0; i < 4; i++)
            {
                int candidate;
                if (i == correctIdx)
                {
                    candidate = answer;
                }
                else
                {
                    int off = Random.Range(-5, 6);
                    if (off == 0) off = 1;
                    candidate = answer + off;
                }
                if (_choiceLabels[i] != null) _choiceLabels[i].text = candidate.ToString();
                int captured = candidate;
                if (_choices[i] != null)
                {
                    _choices[i].onClick.RemoveAllListeners();
                    _choices[i].onClick.AddListener(() => OnChoiceTapped(captured));
                }
            }
        }

        private void OnChoiceTapped(int choice)
        {
            if (!_running) return;
            if (choice == _correctAnswer)
            {
                _score = _score + CorrectGain;
                if (_score.Value > MaxScore) _score.Value = MaxScore;
            }
            else
            {
                _score = _score + WrongPenalty;
            }
            if (_scoreLabel != null) _scoreLabel.text = $"점수: {_score.Value}";
            NextQuestion();
        }

        private void BuildUI()
        {
            _root = new GameObject("MathGeniusUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 점수
            var scoreGo = new GameObject("Score");
            scoreGo.transform.SetParent(_root.transform, false);
            var srt = scoreGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0.85f);
            srt.anchorMax = new Vector2(1, 0.95f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            _scoreLabel = scoreGo.AddComponent<TextMeshProUGUI>();
            _scoreLabel.text = "점수: 0";
            _scoreLabel.fontSize = 48;
            _scoreLabel.alignment = TextAlignmentOptions.Center;
            _scoreLabel.color = Color.white;

            // 질문
            var qGo = new GameObject("Question");
            qGo.transform.SetParent(_root.transform, false);
            var qrt = qGo.AddComponent<RectTransform>();
            qrt.anchorMin = new Vector2(0, 0.6f);
            qrt.anchorMax = new Vector2(1, 0.82f);
            qrt.offsetMin = Vector2.zero;
            qrt.offsetMax = Vector2.zero;
            _question = qGo.AddComponent<TextMeshProUGUI>();
            _question.fontSize = 96;
            _question.alignment = TextAlignmentOptions.Center;
            _question.color = Color.white;

            // 4지선다 (2x2 grid)
            for (int i = 0; i < 4; i++)
            {
                int row = i / 2;
                int col = i % 2;
                var btnGo = new GameObject($"Choice{i}");
                btnGo.transform.SetParent(_root.transform, false);
                var brt = btnGo.AddComponent<RectTransform>();
                float xMin = 0.1f + col * 0.4f + col * 0.05f;
                float xMax = xMin + 0.35f;
                float yMin = 0.15f + (1 - row) * 0.18f;
                float yMax = yMin + 0.15f;
                brt.anchorMin = new Vector2(xMin, yMin);
                brt.anchorMax = new Vector2(xMax, yMax);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var img = btnGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.5f, 0.9f);
                var btn = btnGo.AddComponent<Button>();
                btn.targetGraphic = img;
                _choices[i] = btn;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(btnGo.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var lbl = labelGo.AddComponent<TextMeshProUGUI>();
                lbl.fontSize = 80;
                lbl.alignment = TextAlignmentOptions.Center;
                lbl.color = Color.white;
                _choiceLabels[i] = lbl;
            }
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
