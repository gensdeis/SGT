using System.Collections;
using NUnit.Framework;
using ShortGeta.Core;
using ShortGeta.Minigames.DarkSouls;
using ShortGeta.Minigames.KakaoUnread;
using ShortGeta.Minigames.MathGenius;
using ShortGeta.Minigames.NoodleBoil;
using ShortGeta.Minigames.PokerFace;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShortGeta.Tests.PlayMode
{
    // 5개 미니게임 스모크 테스트.
    // 각 게임을 짧게 launch → ForceEnd → 점수가 음수 아님을 확인 (클램프 작동).
    public class MinigamesSmokeTest
    {
        private IEnumerator RunSmoke<T>() where T : MonoBehaviour, IMinigame
        {
            var go = new GameObject("test");
            var launcher = go.AddComponent<MinigameLauncher>();
            var game = go.AddComponent<T>();

            MinigameResult? captured = null;
            launcher.OnFinished += r => captured = r;

            launcher.Launch(game);
            yield return new WaitForSecondsRealtime(0.2f);
            launcher.ForceEnd();

            Assert.IsNotNull(captured);
            Assert.GreaterOrEqual(captured.Value.Score, 0, "score must be clamped non-negative");
            Assert.IsNotEmpty(captured.Value.GameId);

            Object.Destroy(go);
        }

        [UnityTest] public IEnumerator NoodleBoil() => RunSmoke<NoodleBoilGame>();
        [UnityTest] public IEnumerator PokerFace() => RunSmoke<PokerFaceGame>();
        [UnityTest] public IEnumerator DarkSouls() => RunSmoke<DarkSoulsGame>();
        [UnityTest] public IEnumerator KakaoUnread() => RunSmoke<KakaoUnreadGame>();
        [UnityTest] public IEnumerator MathGenius() => RunSmoke<MathGeniusGame>();
    }
}
