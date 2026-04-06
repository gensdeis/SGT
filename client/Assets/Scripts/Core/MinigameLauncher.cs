using System;
using UnityEngine;

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
            }
        }

        public void ForceEnd()
        {
            if (!_running) return;
            FinishInternal();
        }

        private void Update()
        {
            if (!_running) return;
            if (ElapsedSec >= _current.TimeLimit)
            {
                FinishInternal();
                return;
            }
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

            var result = new MinigameResult
            {
                GameId = _current.GameId,
                Score = score,
                PlayTimeSec = elapsed,
                Cleared = score > 0, // 단순 규칙. 실제 clear 정의는 각 게임이 GetScore 양수로 표현
            };
            _running = false;
            _current = null;
            OnFinished?.Invoke(result);
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
