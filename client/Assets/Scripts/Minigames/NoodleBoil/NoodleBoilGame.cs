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
    public class NoodleBoilGame : MonoBehaviour, IMinigame, IDifficultyAware, IEarlyCompletable
    {
        // 5라운드 모두 완료되면 시간 만료 전이라도 즉시 결과 화면으로
        public bool IsComplete => _roundIdx >= RoundCount && !_filling;
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
                float center = (_sweetSpotMin + _sweetSpotMax) / 2f;
                float dist = Mathf.Abs(p - center);
                float maxDist = (_sweetSpotMax - _sweetSpotMin) / 2f;
                float t = 1f - (dist / maxDist) * 0.3f;
                gain = Mathf.RoundToInt(100f * t);
            }
            _score = _score + gain;
            ShowFeedback(gain);
            EndRound();
        }

        private void ShowFeedback(int gain)
        {
            if (_feedback == null) return;
            if (gain >= 90)      { _feedback.text = "PERFECT!";  _feedback.color = new Color(0.2f, 1f, 0.3f); }
            else if (gain >= 70) { _feedback.text = "Good!";     _feedback.color = new Color(0.9f, 1f, 0.2f); }
            else if (gain > 0)   { _feedback.text = $"+{gain}";  _feedback.color = new Color(1f, 0.8f, 0.2f); }
            else                 { _feedback.text = "불어버렸다!"; _feedback.color = new Color(1f, 0.3f, 0.2f); }
            _feedback.gameObject.SetActive(true);
            _feedbackHideAt = Time.realtimeSinceStartup + 0.8f;
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

            // 피드백 텍스트 자동 숨김
            if (_feedback != null && _feedback.gameObject.activeSelf
                && Time.realtimeSinceStartup >= _feedbackHideAt)
                _feedback.gameObject.SetActive(false);

            if (_filling)
            {
                float p = Progress;
                if (p >= 1f)
                {
                    ShowFeedback(0); // 100% 도달 = 불어버렸다!
                    EndRound();
                }
                if (_bar != null)
                {
                    _bar.fillAmount = p;
                    // 초록 구간 진입 시 바 색 초록, 초과 시 빨강
                    _bar.color = p > _sweetSpotMax
                        ? new Color(1f, 0.2f, 0.1f)
                        : (p >= _sweetSpotMin ? new Color(0.2f, 0.9f, 0.2f) : new Color(1f, 0.45f, 0.10f));
                }
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

        // 피드백 텍스트 (탭 결과)
        private TextMeshProUGUI _feedback;
        private float _feedbackHideAt;

        private void BuildUI()
        {
            _root = new GameObject("NoodleBoilUI");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // ── 배경 ──
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(_root.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            var kitchenSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg(GameId);
            if (kitchenSprite != null) { bgImg.sprite = kitchenSprite; bgImg.color = Color.white; }
            else bgImg.color = new Color(0.55f, 0.38f, 0.22f);

            // ── 라면 팟 이미지 ──
            var potGo = new GameObject("Pot");
            potGo.transform.SetParent(_root.transform, false);
            var potRt = potGo.AddComponent<RectTransform>();
            potRt.anchorMin = new Vector2(0.25f, 0.38f);
            potRt.anchorMax = new Vector2(0.75f, 0.72f);
            potRt.offsetMin = Vector2.zero; potRt.offsetMax = Vector2.zero;
            var potImg = potGo.AddComponent<Image>();
            var potSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadByGameId(GameId, "ramen_pot");
            if (potSprite != null)
            {
                potImg.sprite = potSprite;
                potImg.color = Color.white;
                potImg.preserveAspect = true;
            }
            else
            {
                // 팟 이미지 없으면 원형 냄비 대용
                potImg.color = new Color(0.25f, 0.25f, 0.30f);
                potImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();
                var potEmoji = new GameObject("Emoji"); potEmoji.transform.SetParent(potGo.transform, false);
                var prt2 = potEmoji.AddComponent<RectTransform>();
                prt2.anchorMin = Vector2.zero; prt2.anchorMax = Vector2.one;
                prt2.offsetMin = Vector2.zero; prt2.offsetMax = Vector2.zero;
                var pt = potEmoji.AddComponent<TextMeshProUGUI>();
                pt.text = "🍜"; pt.fontSize = 120; pt.alignment = TextAlignmentOptions.Center;
            }

            // ── 상단 정보 패널 (반투명 배경 + 레이블) ──
            var infoPanelGo = new GameObject("InfoPanel");
            infoPanelGo.transform.SetParent(_root.transform, false);
            var ipRt = infoPanelGo.AddComponent<RectTransform>();
            ipRt.anchorMin = new Vector2(0f, 0.80f);
            ipRt.anchorMax = new Vector2(1f, 1.00f);
            ipRt.offsetMin = Vector2.zero; ipRt.offsetMax = Vector2.zero;
            var ipImg = infoPanelGo.AddComponent<Image>();
            ipImg.color = new Color(0f, 0f, 0f, 0.55f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(infoPanelGo.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.05f, 0f);
            lrt.anchorMax = new Vector2(0.95f, 1f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 40;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;

            // ── 게이지 바 영역 ──
            var barAreaGo = new GameObject("BarArea");
            barAreaGo.transform.SetParent(_root.transform, false);
            var baRt = barAreaGo.AddComponent<RectTransform>();
            baRt.anchorMin = new Vector2(0.05f, 0.24f);
            baRt.anchorMax = new Vector2(0.95f, 0.38f);
            baRt.offsetMin = Vector2.zero; baRt.offsetMax = Vector2.zero;
            var baImg = barAreaGo.AddComponent<Image>();
            baImg.color = new Color(0.10f, 0.10f, 0.10f, 0.80f);
            baImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetRounded(16);
            baImg.type = Image.Type.Sliced;

            // sweet spot (초록 영역) — 바 배경 위에 먼저 그림
            var ssGo = new GameObject("SweetSpot");
            ssGo.transform.SetParent(barAreaGo.transform, false);
            var ssRt = ssGo.AddComponent<RectTransform>();
            ssRt.anchorMin = new Vector2(_sweetSpotMin, 0.05f);
            ssRt.anchorMax = new Vector2(_sweetSpotMax, 0.95f);
            ssRt.offsetMin = Vector2.zero; ssRt.offsetMax = Vector2.zero;
            var ssImg = ssGo.AddComponent<Image>();
            ssImg.color = new Color(0.15f, 0.90f, 0.15f, 0.45f);

            // 바 fill
            var fillGo = new GameObject("BarFill");
            fillGo.transform.SetParent(barAreaGo.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0.05f);
            frt.anchorMax = new Vector2(1f, 0.95f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            _bar = fillGo.AddComponent<Image>();
            _bar.color = new Color(1f, 0.45f, 0.10f);
            _bar.type = Image.Type.Filled;
            _bar.fillMethod = Image.FillMethod.Horizontal;
            _bar.fillAmount = 0f;

            // 바 위 안내 텍스트
            var barLabelGo = new GameObject("BarLabel");
            barLabelGo.transform.SetParent(barAreaGo.transform, false);
            var blrt = barLabelGo.AddComponent<RectTransform>();
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;
            var bl = barLabelGo.AddComponent<TextMeshProUGUI>();
            bl.text = "초록 구간에서 탭!";
            bl.fontSize = 28; bl.alignment = TextAlignmentOptions.Center;
            bl.color = new Color(1f, 1f, 1f, 0.75f);

            // ── 탭 피드백 텍스트 ──
            var fbGo = new GameObject("Feedback");
            fbGo.transform.SetParent(_root.transform, false);
            var fbRt = fbGo.AddComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0.1f, 0.42f);
            fbRt.anchorMax = new Vector2(0.9f, 0.58f);
            fbRt.offsetMin = Vector2.zero; fbRt.offsetMax = Vector2.zero;
            _feedback = fbGo.AddComponent<TextMeshProUGUI>();
            _feedback.fontSize = 72; _feedback.alignment = TextAlignmentOptions.Center;
            _feedback.color = Color.white;
            _feedback.fontStyle = FontStyles.Bold;
            fbGo.SetActive(false);

            // ── 탭 영역 (최상단) ──
            var tapGo = new GameObject("TapArea");
            tapGo.transform.SetParent(_root.transform, false);
            var tapRt = tapGo.AddComponent<RectTransform>();
            tapRt.anchorMin = Vector2.zero; tapRt.anchorMax = Vector2.one;
            tapRt.offsetMin = Vector2.zero; tapRt.offsetMax = Vector2.zero;
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0, 0, 0, 0);
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.onClick.AddListener(() =>
            {
                OnInput(new InputEvent(InputEventType.Down, Vector2.zero, KeyCode.None, Time.realtimeSinceStartup));
            });
            // SetSiblingIndex 없이 마지막에 추가 → 최상위 렌더링
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
