using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    // Iter 3: /v1/me 엔드포인트 클라이언트.
    public class ProfileApi
    {
        private readonly ApiClient _api;
        public ProfileApi(ApiClient api) { _api = api; }

        public UniTask<ProfileResponse> GetMeAsync(CancellationToken ct = default)
            => _api.GetAsync<ProfileResponse>("/v1/me", ct);

        public UniTask<object> UpdateMeAsync(string nickname, int avatarId, CancellationToken ct = default)
        {
            var req = new ProfileUpdateRequest { Nickname = nickname, AvatarId = avatarId };
            return _api.PostJsonAsync<ProfileUpdateRequest, object>("/v1/me", req, ct);
        }
    }
}
