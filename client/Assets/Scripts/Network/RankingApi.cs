using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    public class RankingApi
    {
        private readonly ApiClient _api;
        public RankingApi(ApiClient api) { _api = api; }

        public UniTask<GlobalRankingResponse> GlobalAsync(int limit = 100, CancellationToken ct = default)
        {
            return _api.GetAsync<GlobalRankingResponse>($"/v1/rankings/global?limit={limit}", ct);
        }

        public UniTask<RankingByGameResponse> ByGameAsync(string gameId, int limit = 100, CancellationToken ct = default)
        {
            return _api.GetAsync<RankingByGameResponse>($"/v1/rankings/{gameId}?limit={limit}", ct);
        }
    }
}
