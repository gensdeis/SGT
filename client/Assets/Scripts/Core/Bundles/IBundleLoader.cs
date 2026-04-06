using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ShortGeta.Core.Bundles
{
    // Addressables (또는 동등) 번들 로더 추상화.
    // Iter 2C MVP 에서는 데모 자산 1개를 로드하는 데 사용되며,
    // Iter 2C' 에서 미니게임 prefab 동적 로드까지 확장된다.
    public interface IBundleLoader
    {
        bool IsReady { get; }

        UniTask InitializeAsync();

        UniTask<T> LoadAssetAsync<T>(string address) where T : Object;

        // Addressables 가 반환한 handle 또는 instance 를 reference count 해제.
        // 호출자가 잘못된 객체를 넘기면 noop.
        void Release(object handle);
    }
}
