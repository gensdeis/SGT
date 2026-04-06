using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    public class PurchaseApi
    {
        private readonly ApiClient _api;
        public PurchaseApi(ApiClient api) { _api = api; }

        public UniTask<PurchaseVerifyResponse> VerifyAsync(string productId, string token, CancellationToken ct = default)
        {
            return _api.PostJsonAsync<PurchaseVerifyRequest, PurchaseVerifyResponse>(
                "/v1/purchases/verify",
                new PurchaseVerifyRequest { ProductId = productId, Token = token },
                ct);
        }
    }
}
