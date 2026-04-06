using NUnit.Framework;
using ShortGeta.Core;
using UnityEngine;

namespace ShortGeta.Tests.EditMode
{
    public class MinigameRegistryTests
    {
        // 더미 IMinigame 구현체
        private class DummyGame : MonoBehaviour, IMinigame
        {
            public string GameId => "dummy_v1";
            public string Title => "dummy";
            public string CreatorId => "test";
            public float TimeLimit => 1f;
            public string[] Tags => new[] { "test" };
            public void OnGameStart() { }
            public void OnGameEnd() { }
            public int GetScore() => 0;
            public void OnInput(InputEvent e) { }
        }

        [Test]
        public void Register_And_Contains()
        {
            var reg = new MinigameRegistry();
            reg.Register("dummy_v1", parent => parent.AddComponent<DummyGame>());
            Assert.IsTrue(reg.Contains("dummy_v1"));
            Assert.IsFalse(reg.Contains("missing"));
        }

        [Test]
        public void Create_ReturnsInstance()
        {
            var reg = new MinigameRegistry();
            reg.Register("dummy_v1", parent => parent.AddComponent<DummyGame>());
            var go = new GameObject("test");
            var game = reg.Create("dummy_v1", go);
            Assert.IsNotNull(game);
            Assert.AreEqual("dummy_v1", game.GameId);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Create_Unregistered_Throws()
        {
            var reg = new MinigameRegistry();
            var go = new GameObject("test");
            Assert.Throws<System.InvalidOperationException>(() => reg.Create("missing_v1", go));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RegisteredIds_Contains()
        {
            var reg = new MinigameRegistry();
            reg.Register("a_v1", _ => null);
            reg.Register("b_v1", _ => null);
            Assert.AreEqual(2, reg.RegisteredIds.Count);
        }
    }
}
