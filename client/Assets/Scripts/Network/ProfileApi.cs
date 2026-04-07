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

        // Iter UI v1.3: 게임 통계 (play_count, my_best, favorited)
        public UniTask<GameStatsResponse> GetGameStatsAsync(CancellationToken ct = default)
            => _api.GetAsync<GameStatsResponse>("/v1/me/game-stats", ct);

        // 보관함 토글
        public UniTask<FavoriteResult> AddFavoriteAsync(string gameId, CancellationToken ct = default)
            => _api.PostJsonAsync<object, FavoriteResult>($"/v1/me/favorites/{gameId}", new { }, ct);

        public async UniTask<FavoriteResult> RemoveFavoriteAsync(string gameId, CancellationToken ct = default)
        {
            // ApiClient 에 Delete 가 없어서 raw UnityWebRequest 사용
            using var req = new UnityEngine.Networking.UnityWebRequest(
                _api.BaseUrl + $"/v1/me/favorites/{gameId}", "DELETE");
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(JwtStore.Token))
                req.SetRequestHeader("Authorization", "Bearer " + JwtStore.Token);
            await req.SendWebRequest();
            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                throw new ApiException((long)req.responseCode, req.downloadHandler.text, req.error);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<FavoriteResult>(req.downloadHandler.text);
        }
    }
}
