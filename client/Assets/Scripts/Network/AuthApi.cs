using System.Threading;
using Cysharp.Threading.Tasks;
using ShortGeta.Core;
using UnityEngine;

namespace ShortGeta.Network
{
    public class AuthApi
    {
        private readonly ApiClient _api;
        public AuthApi(ApiClient api) { _api = api; }

        // 디바이스 ID 로 로그인. JWT 저장 + TimeSync 캘리브레이션 (서버 응답 시각 사용).
        public async UniTask<DeviceLoginResponse> LoginByDeviceAsync(string deviceId, CancellationToken ct = default)
        {
            var res = await _api.PostJsonAsync<DeviceLoginRequest, DeviceLoginResponse>(
                "/v1/auth/device",
                new DeviceLoginRequest { DeviceId = deviceId },
                ct);
            JwtStore.Token = res.Token;
            // 서버가 응답에 server time 을 안 실어주므로, 우선 로컬 시각으로 캘리브레이션 (offset=0).
            // Iter 2 에서 별도 /v1/time 또는 응답 헤더 추가 예정.
            TimeSync.Calibrate(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Debug.Log($"[Auth] login OK user_id={res.UserId} ad_removed={res.AdRemoved}");
            return res;
        }
    }
}
