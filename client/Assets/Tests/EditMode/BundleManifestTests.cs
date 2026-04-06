using NUnit.Framework;
using ShortGeta.Core.Bundles;
using ShortGeta.Network;

namespace ShortGeta.Tests.EditMode
{
    public class BundleManifestTests
    {
        [Test]
        public void Empty_Count_Is_Zero()
        {
            var m = new BundleManifest();
            Assert.AreEqual(0, m.Count);
            Assert.AreEqual(0, m.NonEmptyCount);
        }

        [Test]
        public void RegisterFromGameView_Stores_Entry()
        {
            var m = new BundleManifest();
            m.RegisterFromGameView(new GameView
            {
                Id = "frog_catch_v1",
                BundleUrl = "https://cdn.example/frog.bundle",
                BundleVersion = "1",
                BundleHash = "abcd",
            });
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(1, m.NonEmptyCount);
            Assert.IsTrue(m.TryGet("frog_catch_v1", out var e));
            Assert.AreEqual("https://cdn.example/frog.bundle", e.Url);
            Assert.AreEqual("1", e.Version);
            Assert.AreEqual("abcd", e.Hash);
        }

        [Test]
        public void RegisterAll_Mixed_Empty_And_NonEmpty()
        {
            var m = new BundleManifest();
            m.RegisterAll(new[]
            {
                new GameView { Id = "a", BundleUrl = "" },
                new GameView { Id = "b", BundleUrl = "https://cdn/b" },
                new GameView { Id = "c", BundleUrl = null },
            });
            Assert.AreEqual(3, m.Count);
            Assert.AreEqual(1, m.NonEmptyCount);
        }

        [Test]
        public void TryGet_Missing_Returns_False()
        {
            var m = new BundleManifest();
            Assert.IsFalse(m.TryGet("nope", out _));
        }

        [Test]
        public void Clear_Resets()
        {
            var m = new BundleManifest();
            m.RegisterFromGameView(new GameView { Id = "a", BundleUrl = "u" });
            m.Clear();
            Assert.AreEqual(0, m.Count);
        }
    }
}
