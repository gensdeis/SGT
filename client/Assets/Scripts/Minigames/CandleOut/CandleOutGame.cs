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
    public class CandleOutGame : MonoBehaviour, IMinigame, IDifficultyAware, ITimeLimitAware
    {
        public string GameId    => "candle_out_v1";
        public string Title     => "촛불 끄기";
        public string CreatorId => "shotgeta_official";
        public float  TimeLimit => _timeLimit;
        public string[] Tags    => new[] { GameTags.Timing, GameTags.Retro };

        // ── ITimeLimitAware — games.yaml time_limit_sec 주입 ────────────────
        private float _timeLimit = 60f; // 기본값: 서버 데이터 없을 때 fallback
        public void SetTimeLimit(float sec) => _timeLimit = Mathf.Max(5f, sec);

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
        private TextMeshProUGUI _scoreText;
        private TextMeshProUGUI _comboText;

        // ── 촛불 스프라이트 ─────────────────────────────────────────────────
        private Image _candleTableImg;    // 탁자+촛대+촛신 통합 스프라이트 (항상 표시)
        private Image _candleFlameImg;    // 불꽃 (켜짐 시만 표시)
        private Image _windImg;           // 연기/바람 VFX
        private float _windHideAt;

        // ── 수렴 타이머 바 (좌우에서 중앙으로) ─────────────────────────────
        private GameObject    _timerRootGo;
        private RectTransform _timerLeftRt;
        private RectTransform _timerRightRt;
        private Image         _timerLeftImg;
        private Image         _timerRightImg;
        private float         _gameStartAt;

        // ── 패턴 3·4 — Open 후 지연 이벤트 ────────────────────────────────
        private bool  _hadOpeningEvent;   // 이번 라운드 Opening 이벤트 발생 여부
        private bool  _postOpenPending;   // Open 진입 후 지연 이벤트 대기 중
        private float _postOpenFireAt;    // 이벤트 발동 절대 시각
        private bool  _postOpenIsWind;    // true=바람(켜→꺼) / false=손(꺼→켜)

        // ── 손 VFX (패턴 4) ─────────────────────────────────────────────────
        private GameObject    _handGo;
        private RectTransform _handRt;
        private Image         _handImg;   // hand_vfx.png 스프라이트
        private bool          _handAnimating;
        private float         _handAnimStart;
        private const float   HandAnimSec = 0.35f;

        // ── 사운드 ──────────────────────────────────────────────────────────
        private AudioSource _sfx;
        private AudioClip   _clipDoorSlide;
        private AudioClip   _clipCandleOut;
        private AudioClip   _clipCandleLight;
        private AudioClip   _clipWind;

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
            _score       = SafeInt.From(0);
            _running     = true;
            _combo       = 1;
            _gameStartAt = Time.realtimeSinceStartup;
            LoadSounds();
            BuildUI();
            BuildTimerBars();
            EnterState(State.Closed);
        }

        public void OnGameEnd()
        {
            _running     = false;
            _score.Value = Mathf.Clamp(_score.Value, 0, MaxScore);
            if (_root != null)     Destroy(_root);
            if (_timerRootGo != null) Destroy(_timerRootGo);
        }

        public int GetScore()                   => Mathf.Max(0, _score.Value);
        public void OnInput(InputEvent input)   { /* TapArea Button 으로 처리 */ }

        // ── Update ──────────────────────────────────────────────────────────
        private void Update()
        {
            if (!_running) return;
            float now          = Time.realtimeSinceStartup;
            float stateElapsed = now - _stateEnterAt;

            // 바람 VFX 자동 숨김
            if (_windImg != null && _windImg.gameObject.activeSelf && now >= _windHideAt)
                _windImg.gameObject.SetActive(false);

            // 수렴 타이머 업데이트
            UpdateTimerBars();

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
                    // 패턴 3·4: 지연 이벤트 발동
                    if (_postOpenPending && now >= _postOpenFireAt)
                    {
                        _postOpenPending = false;
                        if (_postOpenIsWind)
                        {
                            // 패턴 3: 켜진 촛불 → 바람으로 꺼짐
                            _candleLit = false;
                            UpdateCandleVisual();
                            ShowEventText("💨 바람!");
                            PlaySfx(_clipWind, 0.85f);
                            PlaySfx(_clipCandleOut, 0.9f);
                        }
                        else
                        {
                            // 패턴 4: 꺼진 촛불 → 손이 나와 켜짐
                            _candleLit = true;
                            UpdateCandleVisual();
                            ShowEventText("🤚 손!");
                            StartHandAnim();
                            PlaySfx(_clipCandleLight, 0.9f);
                        }
                    }

                    // 손 슬라이드 애니메이션 갱신
                    UpdateHandAnim(now);

                    if (stateElapsed >= openDuration)
                    {
                        if (!_tappedThisRound && _candleLit)
                        {
                            // 켜진 촛불 놓침 → 콤보 리셋
                            _combo = 1;
                            UpdateScoreUI();
                        }
                        _postOpenPending = false;
                        HideHand();
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
                    _tappedThisRound  = false;
                    _postOpenPending  = false;
                    HideHand();
                    // 다음 라운드 촛불 상태 결정 (60% 켜짐)
                    _candleLit = Random.value < 0.6f;
                    UpdateCandleVisual();
                    break;

                case State.Opening:
                    PlaySfx(_clipDoorSlide, 0.8f);
                    // 패턴 1·2: 문이 열리는 도중 상태 변화 (Opening 이벤트)
                    _hadOpeningEvent = false;
                    if (Random.value < eventProbability)
                    {
                        _hadOpeningEvent = true;
                        if (_candleLit)
                        {
                            _candleLit = false;
                            ShowEventText("💨 바람~");
                            PlaySfx(_clipWind, 0.7f);
                        }
                        else
                        {
                            _candleLit = true;
                            ShowEventText("🖐 손~");
                            PlaySfx(_clipCandleLight, 0.8f);
                        }
                        UpdateCandleVisual();
                    }
                    break;

                case State.Open:
                    // 패턴 3·4: Opening 이벤트가 없었던 라운드에만 Post-Open 이벤트 발동
                    _postOpenPending = false;
                    if (!_hadOpeningEvent && Random.value < eventProbability)
                    {
                        _postOpenPending  = true;
                        _postOpenIsWind   = _candleLit; // 켜짐이면 바람, 꺼짐이면 손
                        // 이벤트 발동 지연: openDuration 의 35% (최소 0.25s)
                        float delay       = Mathf.Max(0.25f, openDuration * 0.35f);
                        _postOpenFireAt   = Time.realtimeSinceStartup + delay;
                    }
                    UpdateCandleVisual();
                    break;

                case State.Closing:
                    _postOpenPending = false;
                    HideHand();
                    break;
            }
        }

        // ── 탭 처리 ─────────────────────────────────────────────────────────
        private void OnTapped()
        {
            if (!_running || _state != State.Open || _tappedThisRound) return;
            _tappedThisRound = true;
            // 탭 시점에 대기 중인 Post-Open 이벤트 취소 (이미 결정된 상태로 판정)
            _postOpenPending = false;
            HideHand();

            if (_candleLit)
            {
                PlaySfx(_clipCandleOut, 1.0f); // 촛불 끄기 성공
                int gain    = BaseScore * _combo;
                _score      = _score + gain;
                if (_score.Value > MaxScore) _score.Value = MaxScore;
                _combo      = Mathf.Min(_combo + 1, MaxCombo);
            }
            else
            {
                _score = _score + penaltyScore;
                _combo = 1;
            }

            UpdateScoreUI();
            EnterState(State.Closing);
        }

        // ── 촛불 비주얼 ─────────────────────────────────────────────────────
        private void UpdateCandleGlow(float now)
        {
            if (_candleFlameImg == null) return;
            if (_candleLit)
            {
                // 불꽃 펄스: alpha + 밝기 진동
                float pulse = 0.85f + 0.15f * Mathf.Sin(now * 4.0f);
                _candleFlameImg.color = new Color(1f, pulse * 0.90f, pulse * 0.55f, 1f);
                _candleFlameImg.gameObject.SetActive(true);
            }
            else
            {
                _candleFlameImg.gameObject.SetActive(false);
            }
        }

        private void UpdateCandleVisual()
        {
            if (_candleFlameImg == null) return;
            _candleFlameImg.gameObject.SetActive(_candleLit);
            if (_candleLit)
                _candleFlameImg.color = new Color(1f, 0.90f, 0.55f);
        }

        // ── 손 VFX (패턴 4) ─────────────────────────────────────────────────
        // 오른쪽 문 뒤에서 손이 미끄러져 나와 촛불 근처까지 이동
        private void StartHandAnim()
        {
            if (_handGo == null) return;
            _handAnimating = true;
            _handAnimStart = Time.realtimeSinceStartup;
            // 시작: 오른쪽 문 안쪽 (x ≈ 0.78~1.00)
            _handRt.anchorMin = new Vector2(0.78f, 0.36f);
            _handRt.anchorMax = new Vector2(1.00f, 0.58f);
            _handRt.offsetMin = _handRt.offsetMax = Vector2.zero;
            _handGo.SetActive(true);
        }

        private void UpdateHandAnim(float now)
        {
            if (!_handAnimating || _handRt == null) return;
            float t = Mathf.Clamp01((now - _handAnimStart) / HandAnimSec);
            // 목표: 촛불 오른쪽 (x ≈ 0.56~0.78) — 손이 촛불 옆까지 뻗어옴
            float xMin = Mathf.Lerp(0.78f, 0.56f, t);
            float xMax = Mathf.Lerp(1.00f, 0.78f, t);
            _handRt.anchorMin = new Vector2(xMin, 0.36f);
            _handRt.anchorMax = new Vector2(xMax, 0.58f);
            _handRt.offsetMin = _handRt.offsetMax = Vector2.zero;
            if (t >= 1f) _handAnimating = false;
        }

        private void HideHand()
        {
            _handAnimating = false;
            if (_handGo != null) _handGo.SetActive(false);
        }

        // ── 문 애니메이션 ────────────────────────────────────────────────────
        // t=0: 닫힘(중앙 3% 오버랩으로 투명 엣지 갭 방지), t=1: 완전 열림(화면 밖)
        private const float DoorOverlap = 0.03f; // 스프라이트 투명 엣지 보정용

        private void SetDoorAnim(float t)
        {
            if (_leftDoorRt == null || _rightDoorRt == null) return;

            float half = 0.5f + DoorOverlap; // = 0.53

            // 왼쪽 문: 닫힘 [0, 0.53] → 열림 [-0.53, 0]
            _leftDoorRt.anchorMin  = new Vector2(Mathf.Lerp(0f,    -half, t), DoorYMin);
            _leftDoorRt.anchorMax  = new Vector2(Mathf.Lerp(half,   0f,   t), DoorYMax);
            _leftDoorRt.offsetMin  = Vector2.zero;
            _leftDoorRt.offsetMax  = Vector2.zero;

            // 오른쪽 문: 닫힘 [0.47, 1] → 열림 [1, 1.53]
            _rightDoorRt.anchorMin = new Vector2(Mathf.Lerp(1f - half,  1f,    t), DoorYMin);
            _rightDoorRt.anchorMax = new Vector2(Mathf.Lerp(1f,         1f+half, t), DoorYMax);
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

        // ── 사운드 로드 & 재생 ───────────────────────────────────────────────
        private void LoadSounds()
        {
            // NOTE: Unity 의 가짜 null(UnityEngine.Object)은 C# ??(null coalescing) 연산자로 감지되지 않음.
            // 반드시 Unity 오버로드된 == null 비교를 사용해야 한다.
            var existing = GetComponent<AudioSource>();
            _sfx = (existing != null) ? existing : gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            _clipDoorSlide   = Resources.Load<AudioClip>("Audio/CandleOut/door_slide");
            _clipCandleOut   = Resources.Load<AudioClip>("Audio/CandleOut/candle_out");
            _clipCandleLight = Resources.Load<AudioClip>("Audio/CandleOut/candle_light");
            _clipWind        = Resources.Load<AudioClip>("Audio/CandleOut/wind_gust");
        }

        private void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (_sfx == null || clip == null) return;
            _sfx.PlayOneShot(clip, volume);
        }

        private void ShowEventText(string msg)
        {
            // 바람 이벤트: wind_smoke 이미지 표시
            if (msg.Contains("바람") && _windImg != null)
            {
                _windImg.gameObject.SetActive(true);
                _windHideAt = Time.realtimeSinceStartup + 0.65f;
            }
            // 손 이벤트: StartHandAnim 에서 처리 (여기서는 아무것도 안 함)
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

            // ── 3. 촛불 — 탁자+촛대+몸통 통합(candle_table) + 불꽃(candle_flame) ─
            // candle_table: 한국 전통 소반 + 황동 촛대 + 촛신 한 장으로
            var tableGo = MakeRect(_root.transform, "CandleTable",
                new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.72f));
            _candleTableImg = tableGo.AddComponent<Image>();
            var tableSpr = ShortGeta.Core.UI.GameSpriteLoader.Load("CandleOut", "candle_table");
            if (tableSpr != null) { _candleTableImg.sprite = tableSpr; _candleTableImg.color = Color.white; }
            else _candleTableImg.color = new Color(0.7f, 0.55f, 0.2f);
            _candleTableImg.preserveAspect = true;

            // 불꽃: 탁자 위 촛심 위치에 (켜짐 시만 표시)
            var flameGo = MakeRect(_root.transform, "CandleFlame",
                new Vector2(0.44f, 0.58f), new Vector2(0.56f, 0.72f));
            _candleFlameImg = flameGo.AddComponent<Image>();
            var flameSpr = ShortGeta.Core.UI.GameSpriteLoader.Load("CandleOut", "candle_flame");
            if (flameSpr != null) { _candleFlameImg.sprite = flameSpr; _candleFlameImg.color = Color.white; }
            else _candleFlameImg.color = new Color(1f, 0.6f, 0.1f);
            _candleFlameImg.preserveAspect = true;

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

            // ── 9. 바람 VFX (바람 이벤트 시 표시) ──────────────────────────
            var windGo = MakeRect(_root.transform, "WindVFX",
                new Vector2(0.05f, 0.50f), new Vector2(0.95f, 0.72f));
            _windImg = windGo.AddComponent<Image>();
            var windSpr = ShortGeta.Core.UI.GameSpriteLoader.Load("CandleOut", "wind_smoke");
            if (windSpr != null) { _windImg.sprite = windSpr; _windImg.color = Color.white; }
            else _windImg.color = new Color(0.7f, 0.85f, 1f, 0.7f);
            _windImg.preserveAspect = true;
            windGo.SetActive(false);

            // ── 10. 손 VFX (패턴 4 전용 — 기본 비활성) ─────────────────────
            _handGo = MakeRect(_root.transform, "HandVFX",
                new Vector2(0.70f, 0.35f), new Vector2(1.05f, 0.62f));
            _handRt = _handGo.GetComponent<RectTransform>();
            _handImg = _handGo.AddComponent<Image>();
            var handSpr = ShortGeta.Core.UI.GameSpriteLoader.Load("CandleOut", "hand_vfx");
            if (handSpr != null) { _handImg.sprite = handSpr; _handImg.color = Color.white; }
            else _handImg.color = new Color(0.9f, 0.72f, 0.55f);
            _handImg.preserveAspect = true;
            _handGo.SetActive(false);

            // ── 11. 탭 영역 — 촛불/문 안쪽 영역에 맞춘 투명 버튼 ────────────
            // 문 안쪽(DoorYMin~DoorYMax)을 덮어 "촛불을 누르는" 느낌을 줌
            var tapGo  = MakeRect(_root.transform, "TapArea",
                new Vector2(0f, DoorYMin), new Vector2(1f, DoorYMax));
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = Color.clear; // 완전 투명
            var tapBtn = tapGo.AddComponent<Button>();
            tapBtn.targetGraphic = tapImg;
            tapBtn.transition    = Selectable.Transition.None;
            tapBtn.onClick.AddListener(OnTapped);
        }

        // ── 문짝 비주얼 빌더 ─────────────────────────────────────────────────
        private void BuildDoorVisual(Transform parent, bool isLeft)
        {
            var body    = MakeChildFill(parent, "Body");
            var bodyImg = body.AddComponent<Image>();

            var doorSprite = ShortGeta.Core.UI.GameSpriteLoader.Load("CandleOut", "door_panel");
            if (doorSprite != null)
            {
                bodyImg.sprite = doorSprite;
                bodyImg.color  = Color.white;
                bodyImg.preserveAspect = false;
                // 오른쪽 문은 X축 반전 (같은 스프라이트를 미러링)
                if (!isLeft)
                {
                    var rt = parent.GetComponent<RectTransform>();
                    if (rt != null)
                        parent.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
            else
            {
                // fallback: 절차적 나무색 문
                bodyImg.color = new Color(0.52f, 0.36f, 0.14f);

                var paper = new GameObject("Paper");
                paper.transform.SetParent(parent, false);
                var pRt = paper.AddComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0.08f, 0.04f);
                pRt.anchorMax = new Vector2(0.92f, 0.96f);
                pRt.offsetMin = Vector2.zero; pRt.offsetMax = Vector2.zero;
                paper.AddComponent<Image>().color = new Color(0.88f, 0.80f, 0.60f, 0.82f);

                var grid = MakeChildFill(paper.transform, "Grid");
                var gt   = grid.AddComponent<TextMeshProUGUI>();
                gt.text  = "─────\n│   │   │\n─────\n│   │   │\n─────\n│   │   │\n─────";
                gt.fontSize = 22; gt.alignment = TextAlignmentOptions.Center;
                gt.color = new Color(0.40f, 0.30f, 0.12f, 0.55f);

                var pillar = new GameObject("Pillar");
                pillar.transform.SetParent(parent, false);
                var plRt = pillar.AddComponent<RectTransform>();
                float pillarX = isLeft ? 0.94f : 0.00f;
                plRt.anchorMin = new Vector2(pillarX, 0f);
                plRt.anchorMax = new Vector2(pillarX + 0.06f, 1f);
                plRt.offsetMin = Vector2.zero; plRt.offsetMax = Vector2.zero;
                pillar.AddComponent<Image>().color = new Color(0.32f, 0.20f, 0.06f);
            }
        }

        // ── 수렴 타이머 바 (좌우에서 중앙으로, MinigameLauncher 바 위에 덮어씀) ──
        private void BuildTimerBars()
        {
            // sortingOrder = 300 으로 MinigameLauncher(200)보다 위에 그림
            var timerRoot = new GameObject("[CandleTimerOverlay]");
            var canvas    = timerRoot.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = timerRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight  = 1f;

            // MinigameLauncher 와 동일한 하단 섹션 크기 (anchorMax.y = 0.035)
            var sectionGo  = new GameObject("Section");
            sectionGo.transform.SetParent(timerRoot.transform, false);
            var sectionRt  = sectionGo.AddComponent<RectTransform>();
            sectionRt.anchorMin = new Vector2(0f, 0f);
            sectionRt.anchorMax = new Vector2(1f, 0.035f);
            sectionRt.offsetMin = Vector2.zero;
            sectionRt.offsetMax = Vector2.zero;

            // 배경 덮기 (MinigameLauncher 의 어두운 바를 그대로 이용)
            var bgImg = sectionGo.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.08f);

            // 왼쪽 바: x=0 → 중앙(0.5)으로 이동
            var leftGo = new GameObject("Left");
            leftGo.transform.SetParent(sectionGo.transform, false);
            _timerLeftRt = leftGo.AddComponent<RectTransform>();
            _timerLeftRt.anchorMin = new Vector2(0f,   0.30f);
            _timerLeftRt.anchorMax = new Vector2(0.5f, 0.70f);
            _timerLeftRt.offsetMin = Vector2.zero;
            _timerLeftRt.offsetMax = Vector2.zero;
            _timerLeftImg = leftGo.AddComponent<Image>();
            _timerLeftImg.color      = new Color(0.25f, 0.85f, 0.35f);
            _timerLeftImg.type       = Image.Type.Filled;
            _timerLeftImg.fillMethod = Image.FillMethod.Horizontal;
            // 왼쪽 바: 왼쪽(x=0) 에서 시작해 중앙(x=0.5)으로 채워짐 → fillOrigin=Left
            _timerLeftImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            _timerLeftImg.fillAmount = 0f; // 처음엔 비어 있다가 시간이 흐를수록 채워짐

            // 오른쪽 바: x=0.5 → 오른쪽 끝(1.0) 방향으로
            var rightGo = new GameObject("Right");
            rightGo.transform.SetParent(sectionGo.transform, false);
            _timerRightRt = rightGo.AddComponent<RectTransform>();
            _timerRightRt.anchorMin = new Vector2(0.5f, 0.30f);
            _timerRightRt.anchorMax = new Vector2(1f,   0.70f);
            _timerRightRt.offsetMin = Vector2.zero;
            _timerRightRt.offsetMax = Vector2.zero;
            _timerRightImg = rightGo.AddComponent<Image>();
            _timerRightImg.color      = new Color(0.25f, 0.85f, 0.35f);
            _timerRightImg.type       = Image.Type.Filled;
            _timerRightImg.fillMethod = Image.FillMethod.Horizontal;
            // 오른쪽 바: 오른쪽(x=1) 에서 시작해 중앙(x=0.5)으로 채워짐 → fillOrigin=Right
            _timerRightImg.fillOrigin = (int)Image.OriginHorizontal.Right;
            _timerRightImg.fillAmount = 0f; // 처음엔 비어 있다가 시간이 흐를수록 채워짐

            // _root 의 자식으로 붙여 세션 종료 시 자동으로 같이 파괴
            timerRoot.transform.SetParent(_root.transform, false);
            _timerRootGo = timerRoot;
        }

        private void UpdateTimerBars()
        {
            if (_timerLeftImg == null || _timerRightImg == null) return;
            float elapsed  = Time.realtimeSinceStartup - _gameStartAt;
            // ratio: 0 → 1 (시간이 흐를수록 증가 → 바가 가장자리에서 중앙으로 채워짐)
            float ratio    = _timeLimit > 0f ? Mathf.Clamp01(elapsed / _timeLimit) : 0f;
            bool  danger   = elapsed >= _timeLimit - 10f;
            var   barColor = danger
                ? new Color(0.95f, 0.25f, 0.20f)   // 10초 이하 → 빨강 (위험!)
                : new Color(0.25f, 0.85f, 0.35f);   // 그 외 → 초록

            _timerLeftImg.fillAmount  = ratio;
            _timerRightImg.fillAmount = ratio;
            _timerLeftImg.color       = barColor;
            _timerRightImg.color      = barColor;
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
