using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.MathGenius
{
    // 수학 천재 도전 — 1자리수 +/- 4지선다.
    //
    // 정답 +50, 오답 -10. 30초 동안 30문제 정답 = 1500 (max).
    public class MathGeniusGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "math_genius_v1";
        public string Title => "수학 천재 도전";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Focus, GameTags.Bmovie };

        private const int MaxScore = 1500;
        private const int CorrectGain = 50;
        private const int WrongPenalty = -10;
        private int _offsetRange = 5; // DDA: -1=3 (가까운 오답, 쉬움), 0=5, +1=8 (혼동 큼)
        private int _difficulty;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
            _offsetRange = _difficulty == -1 ? 3 : (_difficulty == 1 ? 8 : 5);
        }

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
                    int off = Random.Range(-_offsetRange, _offsetRange + 1);
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

            // 칠판 배경 (스프라이트 우선)
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var mathBgImg = bgGo.AddComponent<Image>();
            var chalkSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg("math_genius_v1");
            if (chalkSprite != null) { mathBgImg.sprite = chalkSprite; mathBgImg.color = Color.white; }
            else mathBgImg.color = new Color(0.10f, 0.22f, 0.12f);

            // 점수 (우상단)
            var scoreGo = new GameObject("Score");
            scoreGo.transform.SetParent(_root.transform, false);
            var srt = scoreGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0.88f);
            srt.anchorMax = new Vector2(1, 0.97f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            _scoreLabel = scoreGo.AddComponent<TextMeshProUGUI>();
            _scoreLabel.text = "점수: 0";
            _scoreLabel.fontSize = 44;
            _scoreLabel.alignment = TextAlignmentOptions.Center;
            _scoreLabel.color = new Color(0.95f, 0.95f, 0.85f); // 분필 색

            // 질문 (크게, 중앙 상단)
            var qGo = new GameObject("Question");
            qGo.transform.SetParent(_root.transform, false);
            var qrt = qGo.AddComponent<RectTransform>();
            qrt.anchorMin = new Vector2(0.05f, 0.58f);
            qrt.anchorMax = new Vector2(0.95f, 0.82f);
            qrt.offsetMin = Vector2.zero;
            qrt.offsetMax = Vector2.zero;
            _question = qGo.AddComponent<TextMeshProUGUI>();
            _question.fontSize = 96;
            _question.alignment = TextAlignmentOptions.Center;
            _question.color = Color.white;
            _question.fontStyle = FontStyles.Bold;

            // 4지선다 (2x2 grid) — 4색 라운드
            var btnColors = new[] {
                new Color(0.3f, 0.6f, 0.95f), // 파랑
                new Color(0.2f, 0.75f, 0.5f),  // 민트
                new Color(0.95f, 0.55f, 0.2f), // 주황
                new Color(0.85f, 0.35f, 0.55f), // 핑크
            };
            for (int i = 0; i < 4; i++)
            {
                int row = i / 2;
                int col = i % 2;
                var btnGo = new GameObject($"Choice{i}");
                btnGo.transform.SetParent(_root.transform, false);
                var brt = btnGo.AddComponent<RectTransform>();
                float xMin = 0.08f + col * 0.44f;
                float xMax = xMin + 0.40f;
                float yMin = 0.12f + (1 - row) * 0.20f;
                float yMax = yMin + 0.17f;
                brt.anchorMin = new Vector2(xMin, yMin);
                brt.anchorMax = new Vector2(xMax, yMax);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                var img = btnGo.AddComponent<Image>();
                img.color = btnColors[i];
                img.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetRounded(20);
                img.type = Image.Type.Sliced;
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
                lbl.fontStyle = FontStyles.Bold;
                _choiceLabels[i] = lbl;
            }
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
