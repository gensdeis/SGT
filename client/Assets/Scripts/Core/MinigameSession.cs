using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ShortGeta.Core
{
    // WarioWare 식 자동 전환 세션.
    // 큐에 담긴 미니게임을 순서대로 launch 하고, 게임 사이에 2초 카운트다운을 둔다.
    // 모든 게임이 끝나면 OnSessionFinished 콜백.
    //
    // 사용:
    //   var session = new MinigameSession(launcher);
    //   await session.RunAsync(games, getDelegate);
    public class MinigameSession
    {
        private readonly MinigameLauncher _launcher;

        public MinigameSession(MinigameLauncher launcher)
        {
            _launcher = launcher;
        }

        // games 큐를 순차 실행. each 게임마다 onIntro(gameId, secondsLeft) 콜백으로 카운트다운 표시.
        public async UniTask<List<MinigameResult>> RunAsync(
            IReadOnlyList<IMinigame> games,
            System.Action<string, int> onIntro = null,
            float countdownSec = 2f)
        {
            var results = new List<MinigameResult>(games.Count);
            for (int i = 0; i < games.Count; i++)
            {
                var g = games[i];

                // 카운트다운 (입력 차단은 호출자 UI 가 책임짐)
                for (int s = (int)countdownSec; s > 0; s--)
                {
                    onIntro?.Invoke(g.GameId, s);
                    await UniTask.Delay(System.TimeSpan.FromSeconds(1));
                }
                onIntro?.Invoke(g.GameId, 0);

                // 게임 실행
                var tcs = new UniTaskCompletionSource<MinigameResult>();
                System.Action<MinigameResult> handler = null;
                handler = (r) =>
                {
                    _launcher.OnFinished -= handler;
                    tcs.TrySetResult(r);
                };
                _launcher.OnFinished += handler;
                _launcher.Launch(g);

                var result = await tcs.Task;
                results.Add(result);
                Debug.Log($"[Session] {g.GameId} done score={result.Score} time={result.PlayTimeSec:F1}s");
            }
            return results;
        }
    }
}
