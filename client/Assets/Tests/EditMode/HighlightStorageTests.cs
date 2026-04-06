using System.IO;
using NUnit.Framework;
using ShortGeta.Core.Recording;
using UnityEngine;

namespace ShortGeta.Tests.EditMode
{
    public class HighlightStorageTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "shortgeta-tests-" + System.Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); }
                catch { /* ignore */ }
            }
        }

        [Test]
        public void CreateSessionDir_CreatesUniqueDir()
        {
            var s = new HighlightStorage(_tempDir);
            string d1 = s.CreateSessionDir("frog_catch_v1");
            Assert.IsTrue(Directory.Exists(d1));
            Assert.That(d1, Does.Contain("frog_catch_v1"));
        }

        [Test]
        public void SavePngSequence_WritesAllFrames()
        {
            var s = new HighlightStorage(_tempDir);
            string dir = s.CreateSessionDir("test");
            var frames = new System.Collections.Generic.List<Texture2D>();
            for (int i = 0; i < 5; i++)
            {
                var t = new Texture2D(4, 4);
                for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    t.SetPixel(x, y, Color.red);
                t.Apply();
                frames.Add(t);
            }
            s.SavePngSequence(dir, frames);
            Assert.AreEqual(5, Directory.GetFiles(dir, "frame_*.png").Length);
            foreach (var t in frames) Object.DestroyImmediate(t);
        }

        [Test]
        public void GetLatestSessionDir_ReturnsNewest()
        {
            var s = new HighlightStorage(_tempDir);
            // 시간 순서대로 디렉토리 만들기 위해 짧은 sleep
            string d1 = s.CreateSessionDir("a");
            System.Threading.Thread.Sleep(1100); // timestamp 가 초 단위라 1초 차이 필요
            string d2 = s.CreateSessionDir("b");
            string latest = s.GetLatestSessionDir();
            Assert.AreEqual(d2, latest);
        }

        [Test]
        public void GetLatest_NoSessions_ReturnsNull()
        {
            var s = new HighlightStorage(_tempDir);
            Assert.IsNull(s.GetLatestSessionDir());
        }

        [Test]
        public void TagSanitization_RejectsBadChars()
        {
            var s = new HighlightStorage(_tempDir);
            string d = s.CreateSessionDir("frog/catch:v1");
            Assert.IsTrue(Directory.Exists(d));
            Assert.That(d, Does.Not.Contain("/frog/"));
        }
    }
}
