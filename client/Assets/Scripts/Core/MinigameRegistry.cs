using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShortGeta.Core
{
    // gameId 문자열 → IMinigame 팩토리. 새 미니게임 추가 시
    // BootstrapController 등 비즈니스 코드를 건드리지 않고 등록만 하면 된다.
    //
    // 사용:
    //   var reg = new MinigameRegistry();
    //   reg.Register("frog_catch_v1", parent => parent.AddComponent<FrogCatchGame>());
    //   var game = reg.Create("frog_catch_v1", parentGo);
    public class MinigameRegistry
    {
        private readonly Dictionary<string, Func<GameObject, IMinigame>> _factories = new();

        public void Register(string gameId, Func<GameObject, IMinigame> factory)
        {
            if (string.IsNullOrEmpty(gameId)) throw new ArgumentException("gameId required");
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factories[gameId] = factory;
        }

        public bool Contains(string gameId) => _factories.ContainsKey(gameId);

        public IMinigame Create(string gameId, GameObject parent)
        {
            if (!_factories.TryGetValue(gameId, out var f))
            {
                throw new InvalidOperationException($"MinigameRegistry: unregistered gameId='{gameId}'");
            }
            return f(parent);
        }

        public IReadOnlyCollection<string> RegisteredIds => _factories.Keys;
    }
}
