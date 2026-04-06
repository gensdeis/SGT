using NUnit.Framework;
using ShortGeta.Core;

namespace ShortGeta.Tests.EditMode
{
    public class TimeSyncTests
    {
        [Test]
        public void Calibrate_SetsCalibrated()
        {
            TimeSync.Calibrate(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Assert.IsTrue(TimeSync.IsCalibrated);
        }

        [Test]
        public void GetSyncedTimestamp_NearLocalUtcWhenCalibratedToNow()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            TimeSync.Calibrate(now);
            long synced = TimeSync.GetSyncedTimestamp();
            Assert.That(System.Math.Abs(synced - now), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void GetSyncedTimestamp_AppliesOffset()
        {
            long fakeServer = 1700000000;
            TimeSync.Calibrate(fakeServer);
            long synced = TimeSync.GetSyncedTimestamp();
            // synced 는 fakeServer 근처 (몇 초 이내)
            Assert.That(System.Math.Abs(synced - fakeServer), Is.LessThanOrEqualTo(2));
        }
    }
}
