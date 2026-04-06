using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ShortGeta.Core.Bundles
{
    // com.unity.addressables 기반 IBundleLoader 구현.
    // 패키지 미설치 시 컴파일 에러가 난다 — 필요 시 #if 가드 추가.
    public class AddressableBundleLoader : IBundleLoader
    {
        private bool _initialized;

        public bool IsReady => _initialized;

        public async UniTask InitializeAsync()
        {
            try
            {
                // Addressables 는 첫 호출 시 자동 초기화되지만, 명시 호출이 더 명확.
                var op = Addressables.InitializeAsync(autoReleaseHandle: false);
                await op.ToUniTask();
                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    _initialized = true;
                    Debug.Log("[Bundles] AddressableBundleLoader.InitializeAsync OK");
                }
                else
                {
                    Debug.LogWarning("[Bundles] Addressables init failed");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] InitializeAsync exception: {e.Message}");
            }
        }

        public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object
        {
            var op = Addressables.LoadAssetAsync<T>(address);
            try
            {
                var result = await op.ToUniTask();
                return result;
            }
            catch (System.Exception)
            {
                // op 자체는 reference 가 남아 있을 수 있으므로 release
                Addressables.Release(op);
                throw;
            }
        }

        public void Release(object handle)
        {
            if (handle == null) return;
            try
            {
                if (handle is AsyncOperationHandle aoh)
                {
                    Addressables.Release(aoh);
                }
                else if (handle is Object obj)
                {
                    Addressables.Release(obj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] Release failed: {e.Message}");
            }
        }
    }
}
