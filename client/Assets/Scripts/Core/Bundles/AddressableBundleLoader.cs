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

        /// <summary>
        /// address 에 해당하는 Addressable 이 등록돼 있는지 에러 없이 확인.
        /// InvalidKeyException 은 내부적으로 에러 로그를 남기므로, 로드 전 위치 검사를 먼저 수행.
        /// </summary>
        public async UniTask<bool> ExistsAsync(string address)
        {
            try
            {
                var locOp = Addressables.LoadResourceLocationsAsync(address);
                await locOp.Task;
                bool found = locOp.Status == AsyncOperationStatus.Succeeded
                             && locOp.Result != null
                             && locOp.Result.Count > 0;
                Addressables.Release(locOp);
                return found;
            }
            catch
            {
                return false;
            }
        }

        public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object
        {
            var op = Addressables.LoadAssetAsync<T>(address);
            try
            {
                await op.Task;
                if (op.Status != AsyncOperationStatus.Succeeded)
                    throw new System.Exception($"LoadAssetAsync failed: {address}");
                return op.Result;
            }
            catch (System.Exception)
            {
                // op 자체는 reference 가 남아 있을 수 있으므로 release
                Addressables.Release(op);
                throw;
            }
        }

        public async UniTask LoadCatalogAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[Bundles] LoadCatalogAsync skipped — empty URL");
                return;
            }
            try
            {
                var op = Addressables.LoadContentCatalogAsync(url, autoReleaseHandle: true);
                await op.ToUniTask();
                Debug.Log($"[Bundles] catalog loaded: {url}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] LoadContentCatalogAsync failed for {url}: {e.Message}");
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
