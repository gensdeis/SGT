using System.Collections;
using NUnit.Framework;
using ShortGeta.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShortGeta.Tests.PlayMode
{
    public class MinigameLauncherTests
    {
        // 가짜 미니게임 — 0.2초 동안 점수 +50 으로 증가 후 종료.
        private class FakeGame : MonoBehaviour, IMinigame
        {
            public string GameId => "fake_v1";
            public string Title => "fake";
            public string CreatorId => "test";
            public float TimeLimit => 0.2f;
            public string[] Tags => new[] { "test" };

            public bool Started;
            public bool Ended;
            public int InputCount;
            private int _score;

            public void OnGameStart() { Started = true; _score = 0; }
            public void OnGameEnd() { Ended = true; _score = 50; }
            public int GetScore() => _score;
            public void OnInput(InputEvent e) { InputCount++; }
        }

        [UnityTest]
        public IEnumerator LaunchAndAutoEndOnTimeLimit()
        {
            var go = new GameObject("test");
            var launcher = go.AddComponent<MinigameLauncher>();
            var fake = go.AddComponent<FakeGame>();

            MinigameResult? captured = null;
            launcher.OnFinished += r => captured = r;

            launcher.Launch(fake);
            Assert.IsTrue(fake.Started);
            Assert.IsTrue(launcher.IsRunning);

            // TimeLimit 0.2s + buffer
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsTrue(fake.Ended);
            Assert.IsFalse(launcher.IsRunning);
            Assert.IsNotNull(captured);
            Assert.AreEqual("fake_v1", captured.Value.GameId);
            Assert.AreEqual(50, captured.Value.Score);

            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator ForceEndStopsImmediately()
        {
            var go = new GameObject("test2");
            var launcher = go.AddComponent<MinigameLauncher>();
            var fake = go.AddComponent<FakeGame>();
            MinigameResult? captured = null;
            launcher.OnFinished += r => captured = r;

            launcher.Launch(fake);
            yield return null;
            launcher.ForceEnd();

            Assert.IsFalse(launcher.IsRunning);
            Assert.IsTrue(fake.Ended);
            Assert.IsNotNull(captured);

            Object.Destroy(go);
        }
    }
}
