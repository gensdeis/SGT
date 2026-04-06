using NUnit.Framework;
using ShortGeta.Network;

namespace ShortGeta.Tests.EditMode
{
    public class HmacSignerTests
    {
        private const string Base = "local-dev-hmac-base-key";
        private const string Salt = "local-dev-build-guid";

        [Test]
        public void Sign_Deterministic()
        {
            var s1 = HmacSigner.Sign("frog_catch_v1", 100, 12.5, 1700000000, Base, Salt);
            var s2 = HmacSigner.Sign("frog_catch_v1", 100, 12.5, 1700000000, Base, Salt);
            Assert.AreEqual(s1, s2);
            Assert.AreEqual(64, s1.Length); // sha256 hex
        }

        [Test]
        public void Sign_PerGameUnique()
        {
            var a = HmacSigner.DeriveSecretKey("frog_catch_v1", Base, Salt);
            var b = HmacSigner.DeriveSecretKey("noodle_boil_v1", Base, Salt);
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void Sign_DifferentScoreDifferentSig()
        {
            var s1 = HmacSigner.Sign("g", 100, 10.0, 1700000000, Base, Salt);
            var s2 = HmacSigner.Sign("g", 200, 10.0, 1700000000, Base, Salt);
            Assert.AreNotEqual(s1, s2);
        }

        [Test]
        public void Sign_DifferentTimestampDifferentSig()
        {
            var s1 = HmacSigner.Sign("g", 100, 10.0, 1700000000, Base, Salt);
            var s2 = HmacSigner.Sign("g", 100, 10.0, 1700000001, Base, Salt);
            Assert.AreNotEqual(s1, s2);
        }

        [Test]
        public void Sign_DifferentBuildSaltDifferentSig()
        {
            var s1 = HmacSigner.Sign("g", 100, 10.0, 1700000000, Base, "salt-A");
            var s2 = HmacSigner.Sign("g", 100, 10.0, 1700000000, Base, "salt-B");
            Assert.AreNotEqual(s1, s2);
        }
    }
}
