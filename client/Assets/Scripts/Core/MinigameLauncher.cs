using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.Core
{
    // 단일 미니게임의 라이프사이클을 관리.
    //   1. Launch(IMinigame) 호출 → OnGameStart()
    //   2. Update 루프에서 입력 수집 → IMinigame.OnInput() 전달
    //   3. TimeLimit 도달 또는 외부에서 ForceEnd() 호출 → OnGameEnd()
    //   4. OnFinished 콜백으로 (gameId, score, playTimeSec) 통지
    //
    // 사용 측 (MinigameSession 또는 SessionRunnerController) 은
    // GameObject 에 이 컴포넌트를 attach 하고 Launch() 를 호출한다.
    public class MinigameLauncher : MonoBehaviour
    {
        public Action<MinigameResult> OnFinished;

        private IMinigame _current;
        private float _startedAt;
        private bool _running;

        // 타이머 오버레이
        private GameObject _timerRoot;
        private Image _timerBarFill;

        public bool IsRunning => _running;
        public float ElapsedSec => _running ? Time.realtimeSinceStartup - _startedAt : 0f;
        public IMinigame Current => _current;

        public void Launch(IMinigame minigame)
        {
            if (_running)
            {
                Debug.LogWarning("[MinigameLauncher] already running, force-ending previous");
                ForceEnd();
            }
            _current = minigame ?? throw new ArgumentNullException(nameof(minigame));
            _startedAt = Time.realtimeSinceStartup;
            _running = true;
            try
            {
                _current.OnGameStart();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MinigameLauncher] OnGameStart threw: {e}");
                _running = false;
                _current = null;
                return;
            }
            BuildTimerOverlay(_current.TimeLimit);
        }

        public void ForceEnd()
        {
            if (!_running) return;
            FinishInternal();
        }

        private void Update()
        {
            if (!_running) return;
            float elapsed = ElapsedSec;
            float limit = _current.TimeLimit;

            // 조기 완료 체크 (IEarlyCompletable 구현 게임)
            if (_current is IEarlyCompletable ec && ec.IsComplete)
            {
                UpdateTimerOverlay(0f, limit);
                FinishInternal();
                return;
            }

            if (elapsed >= limit)
            {
                UpdateTimerOverlay(0f, limit);
                FinishInternal();
                return;
            }
            UpdateTimerOverlay(limit - elapsed, limit);
            CollectInputs();
        }

        private void CollectInputs()
        {
            // 마우스 (PC + 에디터)
            if (Input.GetMouseButtonDown(0))
                _current.OnInput(InputEvent.Touch(InputEventType.Down, Input.mousePosition));
            else if (Input.GetMouseButtonUp(0))
                _current.OnInput(InputEvent.Touch(InputEventType.Up, Input.mousePosition));

            // 터치 (모바일) — 0번 터치만 처리 (PERF: 멀티터치는 추후)
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        _current.OnInput(InputEvent.Touch(InputEventType.Down, t.position));
                        break;
                    case TouchPhase.Moved:
                        _current.OnInput(InputEvent.Touch(InputEventType.Move, t.position));
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        _current.OnInput(InputEvent.Touch(InputEventType.Up, t.position));
                        break;
                }
            }
        }

        private void FinishInternal()
        {
            float elapsed = ElapsedSec;
            int score = 0;
            try
            {
                _current.OnGameEnd();
                score = _current.GetScore();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MinigameLauncher] OnGameEnd threw: {e}");
            }

            DestroyTimerOverlay();

            var result = new MinigameResult
            {
                GameId = _current.GameId,
                Score = score,
                PlayTimeSec = elapsed,
                Cleared = score > 0,
            };
            _running = false;
            _current = null;
            OnFinished?.Invoke(result);
        }

        // ── 타이머 오버레이 ──────────────────────────────────────────────

        private void BuildTimerOverlay(float totalSec)
        {
            _timerRoot = new GameObject("[TimerOverlay]");

            var canvas = _timerRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = _timerRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;

            // 하단 전용 섹션 (높이 3.5% ≈ 45px @1280) — 게임 UI 와 시각적으로 분리
            var sectionGo = new GameObject("TimerSection");
            sectionGo.transform.SetParent(_timerRoot.transform, false);
            var sectionRt = sectionGo.AddComponent<RectTransform>();
            sectionRt.anchorMin = new Vector2(0f, 0f);
            sectionRt.anchorMax = new Vector2(1f, 0.035f);
            sectionRt.offsetMin = Vector2.zero;
            sectionRt.offsetMax = Vector2.zero;
            var sectionImg = sectionGo.AddComponent<Image>();
            sectionImg.color = new Color(0.06f, 0.06f, 0.08f); // 거의 검정

            // 얇은 게이지 fill (섹션 내 중앙 세로 40%)
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(sectionGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0.30f);
            fillRt.anchorMax = new Vector2(1f, 0.70f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            _timerBarFill = fillGo.AddComponent<Image>();
            _timerBarFill.color = new Color(0.25f, 0.85f, 0.35f);
            _timerBarFill.type = Image.Type.Filled;
            _timerBarFill.fillMethod = Image.FillMethod.Horizontal;
            _timerBarFill.fillOrigin = (int)Image.OriginHorizontal.Left; // 오른쪽 끝이 점점 왼쪽으로
            _timerBarFill.fillAmount = 1f;
        }

        private void UpdateTimerOverlay(float remaining, float total)
        {
            if (_timerBarFill == null) return;
            _timerBarFill.fillAmount = total > 0f ? remaining / total : 0f;
            _timerBarFill.color = remaining <= 10f
                ? new Color(0.95f, 0.25f, 0.20f)   // 10초 이하 → 빨강
                : new Color(0.25f, 0.85f, 0.35f);   // 그 외 → 초록
        }

        private void DestroyTimerOverlay()
        {
            if (_timerRoot != null)
            {
                Destroy(_timerRoot);
                _timerRoot = null;
                _timerBarFill = null;
            }
        }
    }

    [Serializable]
    public struct MinigameResult
    {
        public string GameId;
        public int Score;
        public float PlayTimeSec;
        public bool Cleared;
    }
}
