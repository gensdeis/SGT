using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ShortGeta.Core.Bundles
{
    // Iter 2C'''': bundle hash 검증.
    // 서버 GameView.bundle_hash (sha256 hex) 와 다운로드된 catalog 파일의 실제 sha256 비교.
    //
    // 사용:
    //   bool ok = await BundleHashVerifier.VerifyAsync(catalogUrl, expectedHashHex);
    //
    // 주의:
    //   - UnityWebRequest 가 응답 전체를 메모리에 올림 — catalog.json 은 작아서 OK,
    //     큰 .bundle 파일은 streaming hash 가 필요 (후속).
    //   - 빈 expected 면 즉시 true 반환 (검증 skip).
    public static class BundleHashVerifier
    {
        public static async UniTask<bool> VerifyAsync(string url, string expectedHashHex)
        {
            if (string.IsNullOrEmpty(expectedHashHex))
            {
                Debug.Log("[Bundles] hash check skipped (no expected hash)");
                return true;
            }
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[Bundles] hash verify failed: empty URL");
                return false;
            }

            using var req = UnityWebRequest.Get(url);
            try
            {
                await req.SendWebRequest();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] hash download failed for {url}: {e.Message}");
                return false;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Bundles] hash download failed for {url}: {req.error}");
                return false;
            }

            byte[] data = req.downloadHandler.data;
            if (data == null || data.Length == 0)
            {
                Debug.LogWarning($"[Bundles] hash download empty: {url}");
                return false;
            }

            string actual = ComputeSha256Hex(data);
            bool match = string.Equals(actual, expectedHashHex, System.StringComparison.OrdinalIgnoreCase);
            if (match)
            {
                Debug.Log($"[Bundles] hash check: expected={expectedHashHex} actual={actual} → PASS");
            }
            else
            {
                Debug.LogWarning($"[Bundles] hash check: expected={expectedHashHex} actual={actual} → MISMATCH");
            }
            return match;
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
