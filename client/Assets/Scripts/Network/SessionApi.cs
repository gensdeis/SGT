using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    public class SessionApi
    {
        private readonly ApiClient _api;
        public SessionApi(ApiClient api) { _api = api; }

        public UniTask<StartSessionResponse> StartAsync(CancellationToken ct = default)
        {
            return _api.PostJsonAsync<object, StartSessionResponse>("/v1/sessions", new { }, ct);
        }

        public UniTask<EndSessionResponse> EndAsync(string sessionId, ScoreSubmission[] scores, CancellationToken ct = default)
        {
            return _api.PostJsonAsync<EndSessionRequest, EndSessionResponse>(
                $"/v1/sessions/{sessionId}/end",
                new EndSessionRequest { Scores = scores },
                ct);
        }
    }
}
