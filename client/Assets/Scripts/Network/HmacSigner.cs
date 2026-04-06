using System;
using System.Security.Cryptography;
using System.Text;

namespace ShortGeta.Network
{
    // BACKEND_PLAN.md §"Anti-cheat — HMAC 동적 키 파생" 의 클라이언트 측 구현.
    //
    // 핵심 원칙:
    //   1. Secret 은 코드에 하드코딩하지 않는다 — 여러 조각을 런타임에 조합한다 (정적 분석 방해)
    //   2. 게임 ID 별로 다른 키를 파생한다 — 키 하나가 노출돼도 피해 최소화
    //   3. 서버 (server/pkg/hmac/hmac.go) 의 DeriveSecretKey 와 동일한 식
    //
    // 서버 식:
    //   HMAC_SHA256(baseKey, gameID || buildSalt)
    //
    // payload:
    //   "{gameID}:{score}:{playTime:.2f}:{timestamp}"
    public static class HmacSigner
    {
        // 정적 분석 난이도 상승을 위해 base 키 조각 분산 (난독화 1차).
        // 추가 상수는 ServerConfig.HmacBaseKey 에서도 mix 됨.
        private static readonly string[] _baseFragments = { "m1n1", "G4me", "S3cr" };

        // 서버의 BUILD_GUID + 클라이언트의 baseFragments 를 조합해서 base key 생성.
        // ServerConfig.HmacBaseKey 와 결합되어 최종 base 가 된다.
        private static byte[] BuildBaseKey(string serverHmacBase)
        {
            string concatFragments = string.Join("-", _baseFragments);
            string combined = concatFragments + ":" + serverHmacBase;
            return Encoding.UTF8.GetBytes(combined);
        }

        // 게임별 secret 파생.
        // serverHmacBase 와 buildGuid 는 ServerConfig 에서 주입.
        public static byte[] DeriveSecretKey(string gameId, string serverHmacBase, string buildGuid)
        {
            using var hmac = new HMACSHA256(BuildBaseKey(serverHmacBase));
            byte[] data = Encoding.UTF8.GetBytes(gameId + buildGuid);
            return hmac.ComputeHash(data);
        }

        // 점수 페이로드 서명. 서버의 hmac.Verify 가 동일 식으로 검증한다.
        public static string Sign(
            string gameId,
            int score,
            double playTime,
            long timestamp,
            string serverHmacBase,
            string buildGuid)
        {
            byte[] secret = DeriveSecretKey(gameId, serverHmacBase, buildGuid);
            string payload = $"{gameId}:{score}:{playTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}:{timestamp}";
            using var hmac = new HMACSHA256(secret);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BytesToHex(hash);
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
