using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ShortGeta.Core.Bundles
{
    // Addressables 패키지가 없거나 초기화 실패 시 사용하는 placeholder.
    // 모든 호출이 noop 또는 throw — 호출자가 try/catch 로 감싸야 함.
    public class StubBundleLoader : IBundleLoader
    {
        public bool IsReady => false;

        public UniTask InitializeAsync()
        {
            Debug.LogWarning("[Bundles] StubBundleLoader — Addressables 미사용");
            return UniTask.CompletedTask;
        }

        public UniTask<T> LoadAssetAsync<T>(string address) where T : Object
        {
            throw new System.NotSupportedException(
                "[Bundles] StubBundleLoader cannot load assets. " +
                "Install com.unity.addressables and use AddressableBundleLoader.");
        }

        public void Release(object handle) { /* noop */ }
    }
}
