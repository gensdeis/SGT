using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.NoodleBoil
{
    // 라면 끓이지 마라 — 5라운드 progress bar 정확도 게임.
    //
    // 각 라운드:
    //   bar 가 0% → 100% 로 ~6초간 차오름
    //   사용자가 60~80% 구간에 탭 → 정확도별 0~100점
    //   80% 초과 = 라면 불음 = 0점
    //   라운드 종료 후 1초 휴식
    //
    // 점수 한계: 5 * 100 = 500 (server max_score=500 일치)
    public class NoodleBoilGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "noodle_boil_v1";
        public string Title => "라면 끓이지 마라";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 45f;
        public string[] Tags => new[] { GameTags.Timing, GameTags.Food, GameTags.Daily };

        private const int MaxScore = 500;
        private const int RoundCount = 5;
        private const float FillSec = 6f;
        private const float RestSec = 1f;

        // DDA 노브: sweet spot 폭. 어려울수록 좁음.
        private float _sweetSpotMin = 0.6f;
        private float _sweetSpotMax = 0.8f;
        private int _difficulty;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
            // -1 = 50%~85% (35% wide), 0 = 60%~80% (20%), +1 = 65%~78% (13%)
            switch (_difficulty)
            {
                case -1: _sweetSpotMin = 0.50f; _sweetSpotMax = 0.85f; break;
                case 1:  _sweetSpotMin = 0.65f; _sweetSpotMax = 0.78f; break;
                default: _sweetSpotMin = 0.60f; _sweetSpotMax = 0.80f; break;
            }
        }

        private SafeInt _score;
        private int _roundIdx;
        private float _phaseStart;
        private bool _filling;        // true = 차오르는 중, false = 휴식
        private bool _running;

        // UI
        private GameObject _root;
        private Image _bar;
        private TextMeshProUGUI _label;

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _roundIdx = 0;
            _running = true;
            BeginFillPhase();
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
            Debug.Log($"[NoodleBoil] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running || !_filling) return;
            if (input.Type != InputEventType.Down) return;

            float p = Progress;
            int gain = 0;
            if (p >= _sweetSpotMin && p <= _sweetSpotMax)
            {
                // sweet spot 정중앙(0.7) 이 100점, 양 끝(0.6/0.8) 이 70점
                float center = (_sweetSpotMin + _sweetSpotMax) / 2f;
                float dist = Mathf.Abs(p - center);
                float maxDist = (_sweetSpotMax - _sweetSpotMin) / 2f;
                float t = 1f - (dist / maxDist) * 0.3f;
                gain = Mathf.RoundToInt(100f * t);
            }
            // sweet spot 외부 = 0점
            _score = _score + gain;
            EndRound();
        }

        private float Progress
        {
            get
            {
                if (!_filling) return 0f;
                return Mathf.Clamp01((Time.realtimeSinceStartup - _phaseStart) / FillSec);
            }
        }

        private void Update()
        {
            if (!_running) return;
            if (_filling)
            {
                if (Progress >= 1f)
                {
                    // 100% 까지 도달 = 라면 불음 = 0점, 다음 라운드
                    EndRound();
                }
                if (_bar != null) _bar.fillAmount = Progress;
            }
            else
            {
                if (Time.realtimeSinceStartup - _phaseStart >= RestSec)
                {
                    if (_roundIdx < RoundCount) BeginFillPhase();
                }
            }
        }

        private void BeginFillPhase()
        {
            _filling = true;
            _phaseStart = Time.realtimeSinceStartup;
            UpdateLabel();
        }

        private void EndRound()
        {
            _filling = false;
            _phaseStart = Time.realtimeSinceStartup;
            _roundIdx++;
            UpdateLabel();
        }

        private void BuildUI()
        {
            _root = new GameObject("NoodleBoilUI");
            _root.transform.SetParent(transform, false);

            // Canvas
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // 따뜻한 주방 배경
            var sceneBg = new GameObject("Bg");
            sceneBg.transform.SetParent(_root.transform, false);
            var sceneBgRt = sceneBg.AddComponent<RectTransform>();
            sceneBgRt.anchorMin = Vector2.zero; sceneBgRt.anchorMax = Vector2.one;
            sceneBgRt.offsetMin = Vector2.zero; sceneBgRt.offsetMax = Vector2.zero;
            sceneBg.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.06f); // 다크 브라운 (주방)

            // Label — 라운드 + 안내
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_root.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0.72f);
            lrt.anchorMax = new Vector2(0.95f, 0.90f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 48;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(1f, 0.92f, 0.75f); // 따뜻한 베이지

            // Bar 배경 — 라운드
            var bgGo = new GameObject("BarBG");
            bgGo.transform.SetParent(_root.transform, false);
            var brt = bgGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.08f, 0.40f);
            brt.anchorMax = new Vector2(0.92f, 0.58f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f);
            bgImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetRounded(16);
            bgImg.type = Image.Type.Sliced;

            // Bar fill
            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(bgGo.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _bar = fillGo.AddComponent<Image>();
            _bar.color = new Color(1f, 0.5f, 0.2f);
            _bar.type = Image.Type.Filled;
            _bar.fillMethod = Image.FillMethod.Horizontal;
            _bar.fillAmount = 0f;

            // Sweet spot indicator
            var ssGo = new GameObject("SweetSpot");
            ssGo.transform.SetParent(bgGo.transform, false);
            var srt = ssGo.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(_sweetSpotMin, 0);
            srt.anchorMax = new Vector2(_sweetSpotMax, 1);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var ssImg = ssGo.AddComponent<Image>();
            ssImg.color = new Color(0.2f, 1f, 0.2f, 0.4f);
            ssGo.transform.SetSiblingIndex(0); // 뒤로

            // Tap area (전체 화면)
            var tapGo = new GameObject("TapArea");
            tapGo.transform.SetParent(_root.transform, false);
            var trt = tapGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0, 0, 0, 0); // 투명하지만 raycast 받음
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.onClick.AddListener(() =>
            {
                OnInput(new InputEvent(InputEventType.Down, Vector2.zero, KeyCode.None, Time.realtimeSinceStartup));
            });
            tapGo.transform.SetSiblingIndex(0);
        }

        private void UpdateLabel()
        {
            if (_label == null) return;
            if (!_running)
            {
                _label.text = $"종료\n{_score.Value} 점";
                return;
            }
            if (_roundIdx >= RoundCount)
            {
                _label.text = $"끝!\n{_score.Value} 점";
                return;
            }
            string state = _filling ? "지금 STOP!" : "다음 라운드...";
            _label.text = $"라면 끓이지 마라\n라운드 {_roundIdx + 1} / {RoundCount}\n{state}\n점수: {_score.Value}";
        }

#if UNITY_EDITOR
        public void __TestForceScore(int s) { _score = SafeInt.From(s); }
#endif
    }
}
