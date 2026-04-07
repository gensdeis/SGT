using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    // Iter 3: /v1/missions 엔드포인트.
    public class MissionsApi
    {
        private readonly ApiClient _api;
        public MissionsApi(ApiClient api) { _api = api; }

        public UniTask<MissionsTodayResponse> TodayAsync(CancellationToken ct = default)
            => _api.GetAsync<MissionsTodayResponse>("/v1/missions/today", ct);

        public UniTask<ClaimResult> ClaimAsync(string missionId, CancellationToken ct = default)
        {
            var req = new MissionClaimRequest { MissionId = missionId };
            return _api.PostJsonAsync<MissionClaimRequest, ClaimResult>("/v1/missions/claim", req, ct);
        }
    }
}
