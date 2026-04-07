using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.DarkSouls
{
    // Dark Souls 도전 — 1/10 확률 운빨 게임.
    //
    // "공격" 버튼 탭 → Random.Range(0,10) == 0 이면 +30, 아니면 "YOU DIED" + 0.7s 입력 락
    // 30s 누적, max 300 (10번 성공)
    public class DarkSoulsGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "dark_souls_v1";
        public string Title => "Dark Souls 도전";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Luck, GameTags.Bmovie, GameTags.Internet };

        private const int MaxScore = 300;
        private const int SuccessGain = 30;
        private const float DeadLockSec = 0.7f;
        private int _successOdds = 10; // 1 in N. DDA: -1=7 (쉬움), 0=10, +1=15 (어려움)
        private int _difficulty;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
            _successOdds = _difficulty == -1 ? 7 : (_difficulty == 1 ? 15 : 10);
        }

        private SafeInt _score;
        private bool _running;
        private bool _locked;
        private float _lockUntil;

        private GameObject _root;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _resultText;

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            _locked = false;
            BuildUI();
            UpdateLabel("");
        }

        public void OnGameEnd()
        {
            _running = false;
            int v = _score.Value;
            if (v < 0) v = 0;
            if (v > MaxScore) v = MaxScore;
            _score.Value = v;
            if (_root != null) Destroy(_root);
            Debug.Log($"[DarkSouls] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            // 자체 버튼 onClick 만 사용
        }

        private void OnAttackTapped()
        {
            if (!_running || _locked) return;
            int r = Random.Range(0, _successOdds);
            if (r == 0)
            {
                _score = _score + SuccessGain;
                if (_score.Value > MaxScore) _score.Value = MaxScore;
                UpdateLabel("승리!");
            }
            else
            {
                _locked = true;
                _lockUntil = Time.realtimeSinceStartup + DeadLockSec;
                UpdateLabel("YOU DIED");
            }
        }

        private void Update()
        {
            if (_locked && Time.realtimeSinceStartup >= _lockUntil)
            {
                _locked = false;
                UpdateLabel("");
            }
        }

        private void BuildUI()
        {
            _root = new GameObject("DarkSoulsUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 결과/안내 텍스트 (상단)
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0.7f);
            lrt.anchorMax = new Vector2(1, 0.95f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 48;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            // 큰 공격 버튼
            var btnGo = new GameObject("AttackButton");
            btnGo.transform.SetParent(_root.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.1f, 0.2f);
            brt.anchorMax = new Vector2(0.9f, 0.6f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(OnAttackTapped);

            var btnLabelGo = new GameObject("AttackLabel");
            btnLabelGo.transform.SetParent(btnGo.transform, false);
            var trt = btnLabelGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _resultText = btnLabelGo.AddComponent<TextMeshProUGUI>();
            _resultText.text = "공격";
            _resultText.fontSize = 100;
            _resultText.alignment = TextAlignmentOptions.Center;
            _resultText.color = Color.white;
        }

        private void UpdateLabel(string status)
        {
            if (_label == null) return;
            _label.text = $"Dark Souls 도전\n점수: {_score.Value}";
            if (_resultText != null)
            {
                if (status == "YOU DIED")
                {
                    _resultText.text = "YOU DIED";
                    _resultText.color = new Color(1f, 0.2f, 0.2f);
                }
                else if (status == "승리!")
                {
                    _resultText.text = "승리!";
                    _resultText.color = new Color(1f, 0.9f, 0.3f);
                }
                else
                {
                    _resultText.text = "공격";
                    _resultText.color = Color.white;
                }
            }
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
