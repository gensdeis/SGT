using System.Threading;
using Cysharp.Threading.Tasks;

namespace ShortGeta.Network
{
    public class GameApi
    {
        private readonly ApiClient _api;
        public GameApi(ApiClient api) { _api = api; }

        public async UniTask<GameView[]> ListAsync(string[] tags = null, CancellationToken ct = default)
        {
            string path = "/v1/games";
            if (tags != null && tags.Length > 0)
            {
                path += "?tags=" + System.Uri.EscapeDataString(string.Join(",", tags));
            }
            var res = await _api.GetAsync<ListGamesResponse>(path, ct);
            return res.Games;
        }

        public UniTask<GameView> GetAsync(string id, CancellationToken ct = default)
        {
            return _api.GetAsync<GameView>("/v1/games/" + id, ct);
        }
    }
}
