using System.Collections.Generic;
using ShortGeta.Network;

namespace ShortGeta.Core.Bundles
{
    // 서버 GameView.bundle_url / version / hash 를 클라이언트에 보관.
    // Iter 2C 에서는 등록만 하고 실제 fetch 는 Iter 2C' 에서 사용.
    public class BundleManifest
    {
        public struct Entry
        {
            public string GameId;
            public string Url;
            public string Version;
            public string Hash;

            public bool IsEmpty => string.IsNullOrEmpty(Url);
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public int Count => _entries.Count;

        public void RegisterFromGameView(GameView g)
        {
            if (g == null || string.IsNullOrEmpty(g.Id)) return;
            _entries[g.Id] = new Entry
            {
                GameId = g.Id,
                Url = g.BundleUrl ?? string.Empty,
                Version = g.BundleVersion ?? string.Empty,
                Hash = g.BundleHash ?? string.Empty,
            };
        }

        public void RegisterAll(IEnumerable<GameView> games)
        {
            if (games == null) return;
            foreach (var g in games) RegisterFromGameView(g);
        }

        public bool TryGet(string gameId, out Entry entry)
        {
            return _entries.TryGetValue(gameId, out entry);
        }

        // 비어 있지 않은 (실제 bundle url 이 있는) 항목 개수
        public int NonEmptyCount
        {
            get
            {
                int n = 0;
                foreach (var kv in _entries)
                {
                    if (!kv.Value.IsEmpty) n++;
                }
                return n;
            }
        }

        public void Clear() => _entries.Clear();
    }
}
