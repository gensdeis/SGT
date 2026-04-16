using ShortGeta.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Minigames.CandleOut
{
    // 촛불 끄기 — 조선시대 테마 타이밍 판단 게임.
    //
    // 루프:
    //   Closed(closeDuration) → Opening(AnimSec) → Open(openDuration) → Closing(AnimSec) → ...
    //
    // 판정:
    //   촛불 켜짐 상태에서 탭 → +30×콤보 (콤보++)
    //   촛불 꺼짐 상태에서 탭 → penaltyScore (-10), 콤보 리셋
    //   촛불 꺼짐 상태에서 무시 → 안전 (콤보 유지)
    //   촛불 켜짐 상태에서 놓침 → 0점, 콤보 리셋
    //
    // 랜덤 이벤트(Opening 진입 시):
    //   바람: 켜짐 → 꺼짐 / 손: 꺼짐 → 켜짐
    public class CandleOutGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId   => "candle_out_v1";
        public string Title    => "촛불 끄기";
        public string CreatorId => "shotgeta_official";
        public float  TimeLimit => 30f;
        public string[] Tags   => new[] { GameTags.Timing, GameTags.Retro };

        // ── Inspector 노출 변수 (기획서 §4.2) ──────────────────────────────
        [SerializeField] private float openDuration     = 1.0f;
        [SerializeField] private float closeDuration    = 1.5f;
        [SerializeField] private float eventProbability = 0.2f;
        [SerializeField] private int   penaltyScore     = -10;

        // ── 상수 ────────────────────────────────────────────────────────────
        private const float AnimSec   = 0.35f; // 문 열림/닫힘 애니메이션 시간
        private const int   BaseScore = 30;
        private const int   MaxCombo  = 5;
        private const int   MaxScore  = 500;

        // 문 닫힘/열림 때 수직 Anchor 범위
        private const float DoorYMin = 0.15f;
        private const float DoorYMax = 0.85f;

        // ── FSM ─────────────────────────────────────────────────────────────
        private enum State { Closed, Opening, Open, Closing }
        private State _state;
        private float _stateEnterAt;

        // ── 게임 상태 ───────────────────────────────────────────────────────
        private bool    _candleLit;
        private bool    _tappedThisRound;
        private int     _combo;
        private SafeInt _score;
        private bool    _running;
        private int     _difficulty;

        // ── UI 레퍼런스 ─────────────────────────────────────────────────────
        private GameObject      _root;
        private RectTransform   _leftDoorRt, _rightDoorRt;
        private Image           _candleImg;
        private Image           _glowImg;       // 문 너머로 비치는 광원
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _comboText;
        private TextMeshProUGUI _feedbackText;
        private TextMeshProUGUI _eventText;
        private TextMeshProUGUI _stateHintText; // "탭!" / "대기..." 안내

        private float _feedbackHideAt;
        private float _eventTextHideAt;

        // ── IDifficultyAware ────────────────────────────────────────────────
        public void SetDifficulty(int i)
        {
            _difficulty = Mathf.Clamp(i, -1, 1);
            switch (_difficulty)
            {
                case -1: openDuration = 1.5f; eventProbability = 0.10f; break;
                case  1: openDuration = 0.6f; eventProbability = 0.35f; break;
                default: openDuration = 1.0f; eventProbability = 0.20f; break;
            }
        }

        // ── IMinigame ───────────────────────────────────────────────────────
        public void OnGameStart()
        {
            _score   = SafeInt.From(0);
            _running = true;
            _combo   = 1;
            BuildUI();
            EnterState(State.Closed);
        }

        public void OnGameEnd()
        {
            _running     = false;
            _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null) Destroy(_root);
        }

        public int GetScore()                   => Mathf.Max(0, _score.Value);
        public void OnInput(InputEvent input)   { /* TapArea Button 으로 처리 */ }

        // ── Update ──────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_running) return;
            float now          = Time.realtimeSinceStartup;
            float stateElapsed = now - _stateEnterAt;

            // 피드백/이벤트 텍스트 자동 숨김
            if (_feedbackText  != null && _feedbackText.gameObject.activeSelf  && now >= _feedbackHideAt)
                _feedbackText.gameObject.SetActive(false);
            if (_eventText     != null && _eventText.gameObject.activeSelf     && now >= _eventTextHideAt)
                _eventText.gameObject.SetActive(false);

            switch (_state)
            {
                case State.Closed:
                    UpdateCandleGlow(now);
                    if (stateElapsed >= closeDuration)
                        EnterState(State.Opening);
                    break;

                case State.Opening:
                {
                    float t = Mathf.Clamp01(stateElapsed / AnimSec);
                    SetDoorAnim(t);
                    if (t >= 1f) EnterState(State.Open);
                    break;
                }

                case State.Open:
                    if (stateElapsed >= openDuration)
                    {
                        if (!_tappedThisRound && _candleLit)
                        {
                            // 켜진 촛불 놓침 → 콤보 리셋
                            _combo = 1;
                            ShowFeedback("놓쳤다!", new Color(1f, 0.65f, 0.1f));
                            UpdateScoreUI();
                        }
                        EnterState(State.Closing);
                    }
                    break;

                case State.Closing:
                {
                    float t = Mathf.Clamp01(stateElapsed / AnimSec);
                    SetDoorAnim(1f - t);
                    if (t >= 1f) EnterState(State.Closed);
                    break;
                }
            }
        }

        // ── FSM 전이 ────────────────────────────────────────────────────────
        private void EnterState(State next)
        {
            _state        = next;
            _stateEnterAt = Time.realtimeSinceStartup;

            switch (next)
            {
                case State.Closed:
                    SetDoorAnim(0f);
                    _tappedThisRound = false;
                    // 다음 라운드 촛불 상태 결정 (60% 켜짐)
                    _candleLit = Random.value < 0.6f;
                    UpdateCandleVisual();
                    SetStateHint("");
                    break;

                case State.Opening:
                    // 랜덤 이벤트 판정 (문이 열리는 중에 상태 변화)
                    if (Random.value < eventProbability)
                    {
                        if (_candleLit)
                        {
                            _candleLit = false;
                            ShowEventText("💨 바람!");
                        }
                        else
                        {
                            _candleLit = true;
                            ShowEventText("🖐 손!");
                        }
                        UpdateCandleVisual();
                    }
                    SetStateHint("");
                    break;

                case State.Open:
                    UpdateCandleVisual();
                    SetStateHint(_candleLit ? "탭!" : "참아!");
                    break;

                case State.Closing:
                    SetStateHint("");
                    break;
            }
        }

        // ── 탭 처리 ─────────────────────────────────────────────────────────
        private void OnTapped()
        {
            if (!_running || _state != State.Open || _tappedThisRound) return;
            _tappedThisRound = true;

            if (_candleLit)
            {
                int gain    = BaseScore * _combo;
                _score      = _score + gain;
                if (_score.Value > MaxScore) _score.Value = MaxScore;
                int prevCombo = _combo;
                _combo      = Mathf.Min(_combo + 1, MaxCombo);

                string msg  = prevCombo >= 3 ? $"COMBO x{prevCombo}!" : "GOOD!";
                ShowFeedback(msg, new Color(0.2f, 1f, 0.45f));
            }
            else
            {
                _score = _score + penaltyScore;
                _combo = 1;
                ShowFeedback("MISS!", new Color(1f, 0.25f, 0.2f));
            }

            UpdateScoreUI();
            EnterState(State.Closing);
        }

        // ── 촛불 비주얼 ─────────────────────────────────────────────────────
        private void UpdateCandleGlow(float now)
        {
            if (_glowImg == null || _candleImg == null) return;

            if (_candleLit)
            {
                // 촛불: 따뜻한 주황 펄스
                float g = 0.65f + 0.35f * Mathf.Sin(now * 3.2f);
                _candleImg.color = new Color(1f, g * 0.82f, 0.04f);
                // 문 너머 광원: 부드러운 황갈 오버레이
                float gA = 0.12f + 0.10f * Mathf.Sin(now * 3.2f + 0.5f);
                _glowImg.color = new Color(1f, 0.72f, 0.1f, gA);
            }
            else
            {
                _candleImg.color = new Color(0.20f, 0.20f, 0.24f);
                _glowImg.color   = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void UpdateCandleVisual()
        {
            if (_candleImg == null) return;
            _candleImg.color = _candleLit
                ? new Color(1f, 0.72f, 0.04f)
                : new Color(0.20f, 0.20f, 0.24f);
            if (_glowImg != null)
                _glowImg.color = _candleLit
                    ? new Color(1f, 0.72f, 0.1f, 0.15f)
                    : new Color(0f, 0f, 0f, 0f);
        }

        // ── 문 애니메이션 ────────────────────────────────────────────────────
        // t=0: 닫힘(정중앙 맞닿음), t=1: 완전 열림(화면 밖)
        private void SetDoorAnim(float t)
        {
            if (_leftDoorRt == null || _rightDoorRt == null) return;

            // 왼쪽 문: 닫힘 anchorX 0~0.5 → 열림 -0.5~0
            _leftDoorRt.anchorMin  = new Vector2(Mathf.Lerp(0f,   -0.5f, t), DoorYMin);
            _leftDoorRt.anchorMax  = new Vector2(Mathf.Lerp(0.5f,  0f,   t), DoorYMax);
            _leftDoorRt.offsetMin  = Vector2.zero;
            _leftDoorRt.offsetMax  = Vector2.zero;

            // 오른쪽 문: 닫힘 anchorX 0.5~1 → 열림 1~1.5
            _rightDoorRt.anchorMin = new Vector2(Mathf.Lerp(0.5f,  1f,   t), DoorYMin);
            _rightDoorRt.anchorMax = new Vector2(Mathf.Lerp(1f,    1.5f, t), DoorYMax);
            _rightDoorRt.offsetMin = Vector2.zero;
            _rightDoorRt.offsetMax = Vector2.zero;
        }

        // ── UI 갱신 헬퍼 ────────────────────────────────────────────────────
        private void UpdateScoreUI()
        {
            if (_scoreText != null)
                _scoreText.text = $"점수: {Mathf.Max(0, _score.Value)}";
            if (_comboText != null)
                _comboText.text = _combo > 1 ? $"콤보 x{_combo}" : "";
        }

        private void SetStateHint(string msg)
        {
            if (_stateHintText != null)
                _stateHintText.text = msg;
        }

        private void ShowFeedback(string msg, Color col)
        {
            if (_feedbackText == null) return;
            _feedbackText.text  = msg;
            _feedbackText.color = col;
            _feedbackText.gameObject.SetActive(true);
            _feedbackHideAt = Time.realtimeSinceStartup + 0.75f;
        }

        private void ShowEventText(string msg)
        {
            if (_eventText == null) return;
            _eventText.text = msg;
            _eventText.gameObject.SetActive(true);
            _eventTextHideAt = Time.realtimeSinceStartup + 0.65f;
        }

        // ── BuildUI ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            _root = new GameObject("CandleOutUI");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(720, 1280);
            scaler.matchWidthOrHeight   = 1f;
            _root.AddComponent<GraphicRaycaster>();

            // ── 1. 배경 ──────────────────────────────────────────────────────
            var bgGo  = MakeFullRect(_root.transform, "Bg");
            var bgImg = bgGo.AddComponent<Image>();
            var bgSprite = ShortGeta.Core.UI.GameSpriteLoader.LoadBg(GameId);
            if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.color = Color.white; }
            else bgImg.color = new Color(0.09f, 0.06f, 0.02f); // 다크 앰버 (한지방 밤)

            // ── 2. 방바닥 그라디언트 더미 ────────────────────────────────────
            var floorGo  = MakeRect(_root.transform, "Floor", new Vector2(0f, 0f), new Vector2(1f, 0.20f));
            var floorImg = floorGo.AddComponent<Image>();
            floorImg.color = new Color(0.18f, 0.10f, 0.04f, 0.85f);

            // ── 3. 광원 오버레이 (문 너머 촛불 빛) ──────────────────────────
            // 문 영역(DoorYMin~DoorYMax) 중앙에만 걸리는 타원형 느낌을 직사각으로 근사
            var glowGo  = MakeRect(_root.transform, "GlowLayer",
                new Vector2(0.20f, DoorYMin), new Vector2(0.80f, DoorYMax));
            _glowImg = glowGo.AddComponent<Image>();
            _glowImg.color = new Color(0f, 0f, 0f, 0f); // 초기값: 투명
            _glowImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();

            // ── 4. 촛불 ──────────────────────────────────────────────────────
            var candleGo = MakeRect(_root.transform, "Candle",
                new Vector2(0.37f, 0.36f), new Vector2(0.63f, 0.62f));
            _candleImg = candleGo.AddComponent<Image>();
            _candleImg.sprite = ShortGeta.Core.UI.RoundedSpriteFactory.GetCircle();
            _candleImg.color  = new Color(1f, 0.72f, 0.04f);

            var candleEmojiGo = MakeChildFill(candleGo.transform, "CandleEmoji");
            var cet = candleEmojiGo.AddComponent<TextMeshProUGUI>();
            cet.text = "🕯"; cet.fontSize = 72; cet.alignment = TextAlignmentOptions.Center;

            // ── 5. 왼쪽 문짝 ─────────────────────────────────────────────────
            var leftDoor = new GameObject("DoorLeft");
            leftDoor.transform.SetParent(_root.transform, false);
            _leftDoorRt = leftDoor.AddComponent<RectTransform>();
            BuildDoorVisual(leftDoor.transform, isLeft: true);

            // ── 6. 오른쪽 문짝 ───────────────────────────────────────────────
            var rightDoor = new GameObject("DoorRight");
            rightDoor.transform.SetParent(_root.transform, false);
            _rightDoorRt = rightDoor.AddComponent<RectTransform>();
            BuildDoorVisual(rightDoor.transform, isLeft: false);

            // 초기 문 위치 (닫힘)
            SetDoorAnim(0f);

            // ── 7. 문틀 (위아래 가로 바) ─────────────────────────────────────
            var frameTop = MakeRect(_root.transform, "FrameTop",
                new Vector2(0f, DoorYMax - 0.01f), new Vector2(1f, DoorYMax + 0.01f));
            frameTop.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.08f);

            var frameBot = MakeRect(_root.transform, "FrameBot",
                new Vector2(0f, DoorYMin - 0.01f), new Vector2(1f, DoorYMin + 0.01f));
            frameBot.AddComponent<Image>().color = new Color(0.35f, 0.22f, 0.08f);

            // ── 8. 상단 정보 패널 ────────────────────────────────────────────
            var infoGo = MakeRect(_root.transform, "InfoPanel",
                new Vector2(0f, 0.86f), new Vector2(1f, 1.00f));
            infoGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);

            var titleGo = MakeRect(infoGo.transform, "Title",
                new Vector2(0.04f, 0.5f), new Vector2(0.52f, 1f));
            var titleT = titleGo.AddComponent<TextMeshProUGUI>();
            titleT.text = "촛불 끄기"; titleT.fontSize = 30;
            titleT.alignment = TextAlignmentOptions.MidlineLeft; titleT.color = new Color(1f, 0.88f, 0.55f);

            var scoreGo = MakeRect(infoGo.transform, "Score",
                new Vector2(0.04f, 0f), new Vector2(0.55f, 0.55f));
            _scoreText = scoreGo.AddComponent<TextMeshProUGUI>();
            _scoreText.text = "점수: 0"; _scoreText.fontSize = 28;
            _scoreText.alignment = TextAlignmentOptions.MidlineLeft; _scoreText.color = Color.white;

            var comboGo = MakeRect(infoGo.transform, "Combo",
                new Vector2(0.55f, 0f), new Vector2(0.98f, 1f));
            _comboText = comboGo.AddComponent<TextMeshProUGUI>();
            _comboText.fontSize = 28; _comboText.fontStyle = FontStyles.Bold;
            _comboText.alignment = TextAlignmentOptions.MidlineRight;
            _comboText.color = new Color(1f, 0.92f, 0.25f);

            // ── 9. 상태 힌트 (탭! / 참아!) — 촛불 바로 위, 문 안쪽 ───────────
            var hintGo = MakeRect(_root.transform, "StateHint",
                new Vector2(0.10f, 0.65f), new Vector2(0.90f, 0.76f));
            _stateHintText = hintGo.AddComponent<TextMeshProUGUI>();
            _stateHintText.fontSize = 52; _stateHintText.fontStyle = FontStyles.Bold;
            _stateHintText.alignment = TextAlignmentOptions.Center;
            _stateHintText.color = new Color(1f, 0.95f, 0.35f);

            // ── 10. 이벤트 텍스트 — 문 상단부 안쪽 ─────────────────────────
            var eventGo = MakeRect(_root.transform, "EventText",
                new Vector2(0.10f, 0.73f), new Vector2(0.90f, 0.83f));
            _eventText = eventGo.AddComponent<TextMeshProUGUI>();
            _eventText.fontSize = 44; _eventText.fontStyle = FontStyles.Bold;
            _eventText.alignment = TextAlignmentOptions.Center;
            _eventText.color = new Color(1f, 0.88f, 0.45f);
            eventGo.SetActive(false);

            // ── 11. 탭 결과 피드백 — 화면 하단 전용 영역 ───────────────────
            var fbGo = MakeRect(_root.transform, "Feedback",
                new Vector2(0.10f, 0.06f), new Vector2(0.90f, 0.15f));
            _feedbackText = fbGo.AddComponent<TextMeshProUGUI>();
            _feedbackText.fontSize = 64; _feedbackText.fontStyle = FontStyles.Bold;
            _feedbackText.alignment = TextAlignmentOptions.Center;
            fbGo.SetActive(false);

            // ── 12. 탭 영역 (최상단 투명 버튼) ──────────────────────────────
            var tapGo  = MakeFullRect(_root.transform, "TapArea");
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0, 0, 0, 0);
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.transition    = Selectable.Transition.None;
            tapBtn.onClick.AddListener(OnTapped);
        }

        // ── 문짝 비주얼 빌더 ─────────────────────────────────────────────────
        private void BuildDoorVisual(Transform parent, bool isLeft)
        {
            // 문 본체 — 나무 갈색
            var body = MakeChildFill(parent, "Body");
            body.AddComponent<Image>().color = new Color(0.52f, 0.36f, 0.14f);

            // 한지 내부 패널 (약간 연한 색)
            var paper = new GameObject("Paper");
            paper.transform.SetParent(parent, false);
            var pRt = paper.AddComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.08f, 0.04f);
            pRt.anchorMax = new Vector2(0.92f, 0.96f);
            pRt.offsetMin = Vector2.zero; pRt.offsetMax = Vector2.zero;
            paper.AddComponent<Image>().color = new Color(0.88f, 0.80f, 0.60f, 0.82f);

            // 격자 살 (텍스트로 시뮬레이션)
            var grid = MakeChildFill(paper.transform, "Grid");
            var gt   = grid.AddComponent<TextMeshProUGUI>();
            gt.text  = "─────\n│   │   │\n─────\n│   │   │\n─────\n│   │   │\n─────";
            gt.fontSize = 22; gt.alignment = TextAlignmentOptions.Center;
            gt.color = new Color(0.40f, 0.30f, 0.12f, 0.55f);

            // 가운데 세로 기둥 (문짝 경계)
            var pillar = new GameObject("Pillar");
            pillar.transform.SetParent(parent, false);
            var plRt = pillar.AddComponent<RectTransform>();
            float pillarX = isLeft ? 0.94f : 0.00f;
            plRt.anchorMin = new Vector2(pillarX, 0f);
            plRt.anchorMax = new Vector2(pillarX + 0.06f, 1f);
            plRt.offsetMin = Vector2.zero; plRt.offsetMax = Vector2.zero;
            pillar.AddComponent<Image>().color = new Color(0.32f, 0.20f, 0.06f);
        }

        // ── RectTransform 유틸 ──────────────────────────────────────────────
        private static GameObject MakeFullRect(Transform parent, string name)
            => MakeRect(parent, name, Vector2.zero, Vector2.one);

        private static GameObject MakeRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin  = anchorMin; rt.anchorMax  = anchorMax;
            rt.offsetMin  = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject MakeChildFill(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }
    }
}
