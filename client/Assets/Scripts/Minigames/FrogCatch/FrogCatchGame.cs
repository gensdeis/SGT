using System.Collections.Generic;
using ShortGeta.Core;
using UnityEngine;

namespace ShortGeta.Minigames.FrogCatch
{
    // 개구리 잡아라 — 숏게타 오리지널 미니게임 1번.
    //
    // 규칙:
    //   - 30초 동안 화면에 랜덤 등장하는 개구리를 탭
    //   - 탭 성공 +10
    //   - 탭 실패(빈 화면 탭) -2
    //   - 개구리가 사라지기 전에 못 잡으면 0 (페널티 없음)
    //   - 최대 점수는 server games.yaml 의 max_score=1000 안쪽
    //
    // 9:16 세로. URP camera 가 정방향 정면을 비추는 것을 가정.
    public class FrogCatchGame : MonoBehaviour, IMinigame, IDifficultyAware
    {
        public string GameId => "frog_catch_v1";
        public string Title => "개구리 잡아라";
        public string CreatorId => "shotgeta_official";
        public float TimeLimit => 30f;
        public string[] Tags => new[] { GameTags.Reflex, GameTags.Animal, GameTags.Bmovie };

        [SerializeField] private FrogSpawner spawner;
        [SerializeField] private Camera worldCamera;

        private SafeInt _score;
        private bool _running;
        private int _difficulty;

        public void SetDifficulty(int intensity)
        {
            _difficulty = Mathf.Clamp(intensity, -1, 1);
        }

        private static readonly int ScorePerCatch = 10;
        private static readonly int ScorePerMiss = -2;

        public void OnGameStart()
        {
            _score = SafeInt.From(0);
            _running = true;
            if (spawner != null)
            {
                // -1 = 1.5x (느림), 0 = 1.0x, +1 = 0.7x (빠름)
                float mult = _difficulty == -1 ? 1.5f : (_difficulty == 1 ? 0.7f : 1f);
                spawner.SetSpawnIntervalMultiplier(mult);
                spawner.Begin(OnFrogCaught, OnFrogMissed);
            }
            Debug.Log($"[FrogCatch] start difficulty={_difficulty}");
        }

        public void OnGameEnd()
        {
            _running = false;
            if (spawner != null) spawner.Stop();
            // 음수 점수 방지
            if (_score.Value < 0) _score.Value = 0;
            Debug.Log($"[FrogCatch] end score={_score.Value}");
        }

        public int GetScore() => _score.Value;

        public void OnInput(InputEvent input)
        {
            if (!_running) return;
            if (input.Type != InputEventType.Down) return;
            if (spawner == null) return;

            // 빈 화면 탭이면 -2.
            // FrogSpawner 가 자체 collider 로 onClick 처리하므로
            // 여기서는 "어디든 Down 되면 일단 miss 후보로 생각", 잡힌 frog 가 있으면 OnFrogCaught 가 +10 으로 보정.
            //
            // 단순화: tap 자체로는 점수 변화 없고, FrogSpawner 가 hit/miss 를 알려준다.
        }

        private void OnFrogCaught()
        {
            _score = _score + ScorePerCatch;
            // 상한 클램프 (서버 max_score=1000 보호)
            if (_score.Value > 1000) _score.Value = 1000;
        }

        private void OnFrogMissed()
        {
            _score = _score + ScorePerMiss;
        }

#if UNITY_EDITOR
        // 테스트 헬퍼: 외부에서 spawner/camera 주입 가능.
        public void __TestSetSpawner(FrogSpawner s) { spawner = s; }
#endif
    }
}
