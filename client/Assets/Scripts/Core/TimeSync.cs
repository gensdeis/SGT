using System;
using UnityEngine;

namespace ShortGeta.Core
{
    // 서버 시간 동기화. 클라이언트의 로컬 시계는 변조 가능하므로,
    // 점수 제출 시 timestamp 는 항상 GetSyncedTimestamp() 사용.
    //
    // 흐름:
    //   1. 서버 응답에서 server time 추출 (예: HTTP Date 헤더 또는 /v1/auth/device 응답 필드)
    //   2. Calibrate(serverEpochSec) 호출 → offset 저장
    //   3. 이후 GetSyncedTimestamp() 는 (Time.realtimeSinceStartup 기준 + offset) 반환
    //
    // BACKEND_PLAN.md §"Anti-cheat" Replay window 30s 와 짝.
    public static class TimeSync
    {
        private static double _offsetSec; // server - local
        private static bool _calibrated;

        public static bool IsCalibrated => _calibrated;

        public static void Calibrate(long serverEpochSec)
        {
            double localNow = LocalUnixSec();
            _offsetSec = serverEpochSec - localNow;
            _calibrated = true;
            Debug.Log($"[TimeSync] calibrated, offset={_offsetSec:F2}s");
        }

        public static long GetSyncedTimestamp()
        {
            return (long)(LocalUnixSec() + _offsetSec);
        }

        private static double LocalUnixSec()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
