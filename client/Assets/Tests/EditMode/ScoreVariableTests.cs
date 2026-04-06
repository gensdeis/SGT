using NUnit.Framework;
using ShortGeta.Core;

namespace ShortGeta.Tests.EditMode
{
    public class ScoreVariableTests
    {
        [Test]
        public void SafeInt_RoundTrip()
        {
            var s = SafeInt.From(42);
            Assert.AreEqual(42, s.Value);
        }

        [Test]
        public void SafeInt_Set()
        {
            var s = SafeInt.From(0);
            s.Value = 999;
            Assert.AreEqual(999, s.Value);
        }

        [Test]
        public void SafeInt_PlusOperator()
        {
            var s = SafeInt.From(10);
            s = s + 5;
            Assert.AreEqual(15, s.Value);
        }

        [Test]
        public void SafeInt_MinusOperator()
        {
            var s = SafeInt.From(10);
            s = s - 3;
            Assert.AreEqual(7, s.Value);
        }

        [Test]
        public void SafeInt_NegativeValues()
        {
            var s = SafeInt.From(-100);
            Assert.AreEqual(-100, s.Value);
        }

        [Test]
        public void SafeFloat_RoundTrip()
        {
            var s = SafeFloat.From(3.14f);
            Assert.AreEqual(3.14f, s.Value, 0.0001f);
        }

        [Test]
        public void SafeFloat_NegativeAndZero()
        {
            var a = SafeFloat.From(0f);
            Assert.AreEqual(0f, a.Value);
            var b = SafeFloat.From(-1.5f);
            Assert.AreEqual(-1.5f, b.Value, 0.0001f);
        }
    }
}
