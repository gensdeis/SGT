using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    // Iter 3: /v1/share/claim 엔드포인트.
    public class ShareApi
    {
        private readonly ApiClient _api;
        public ShareApi(ApiClient api) { _api = api; }

        public UniTask<ClaimResult> ClaimAsync(string platform, string highlightTag, CancellationToken ct = default)
        {
            var req = new ShareClaimRequest { Platform = platform, HighlightTag = highlightTag };
            return _api.PostJsonAsync<ShareClaimRequest, ClaimResult>("/v1/share/claim", req, ct);
        }
    }
}
