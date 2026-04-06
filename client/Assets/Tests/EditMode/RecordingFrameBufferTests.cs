using NUnit.Framework;
using ShortGeta.Core;
using UnityEngine;

namespace ShortGeta.Tests.EditMode
{
    public class RecordingFrameBufferTests
    {
        private static Texture2D MakeTex(byte tag)
        {
            var t = new Texture2D(2, 2);
            t.SetPixel(0, 0, new Color32(tag, tag, tag, 255));
            t.Apply();
            return t;
        }

        [Test]
        public void Empty_Snapshot_ReturnsZero()
        {
            var buf = new RecordingFrameBuffer(5);
            Assert.AreEqual(0, buf.Count);
            Assert.AreEqual(0, buf.Snapshot().Count);
        }

        [Test]
        public void PushUnderCapacity_PreservesOrder()
        {
            var buf = new RecordingFrameBuffer(5);
            buf.Push(MakeTex(1));
            buf.Push(MakeTex(2));
            buf.Push(MakeTex(3));
            var snap = buf.Snapshot();
            Assert.AreEqual(3, snap.Count);
            Assert.AreEqual(1, snap[0].GetPixel(0, 0).r * 255);
            Assert.AreEqual(2, snap[1].GetPixel(0, 0).r * 255);
            Assert.AreEqual(3, snap[2].GetPixel(0, 0).r * 255);
            buf.Clear();
        }

        [Test]
        public void PushOverCapacity_DropsOldest()
        {
            var buf = new RecordingFrameBuffer(3);
            buf.Push(MakeTex(1));
            buf.Push(MakeTex(2));
            buf.Push(MakeTex(3));
            buf.Push(MakeTex(4));
            buf.Push(MakeTex(5));
            // 1, 2 dropped — keep 3, 4, 5 (시간순)
            var snap = buf.Snapshot();
            Assert.AreEqual(3, snap.Count);
            Assert.AreEqual(3, snap[0].GetPixel(0, 0).r * 255);
            Assert.AreEqual(4, snap[1].GetPixel(0, 0).r * 255);
            Assert.AreEqual(5, snap[2].GetPixel(0, 0).r * 255);
            buf.Clear();
        }

        [Test]
        public void Clear_ResetsCount()
        {
            var buf = new RecordingFrameBuffer(3);
            buf.Push(MakeTex(1));
            buf.Push(MakeTex(2));
            buf.Clear();
            Assert.AreEqual(0, buf.Count);
            Assert.AreEqual(0, buf.Snapshot().Count);
        }

        [Test]
        public void InvalidCapacity_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new RecordingFrameBuffer(0));
            Assert.Throws<System.ArgumentException>(() => new RecordingFrameBuffer(-1));
        }
    }
}
