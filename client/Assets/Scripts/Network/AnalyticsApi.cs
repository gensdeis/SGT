using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    public class AnalyticsApi
    {
        private readonly ApiClient _api;
        public AnalyticsApi(ApiClient api) { _api = api; }

        // fire-and-forget 패턴으로 호출 권장: api.EventAsync(...).Forget();
        public UniTask EventAsync(string gameId, string eventType, object payload = null, CancellationToken ct = default)
        {
            return _api.PostJsonNoResponseAsync(
                "/v1/analytics/event",
                new AnalyticsEventRequest { GameId = gameId, EventType = eventType, Payload = payload ?? new { } },
                ct);
        }
    }
}
