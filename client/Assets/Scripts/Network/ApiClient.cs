using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ShortGeta.Network
{
    // UnityWebRequest 를 UniTask 로 wrapping 한 단일 HTTP 클라이언트.
    // 모든 *Api 클래스가 이걸 사용한다.
    //
    // 책임:
    //   - JWT 자동 헤더 주입 (JwtStore.Token)
    //   - JSON serialize/deserialize (Newtonsoft)
    //   - 5초 (또는 config) 타임아웃
    //   - HTTP 5xx / 네트워크 오류는 ApiException 으로 변환
    public class ApiException : Exception
    {
        public long StatusCode;
        public string Body;
        public ApiException(long status, string body, string msg) : base(msg)
        {
            StatusCode = status;
            Body = body;
        }
    }

    public class ApiClient
    {
        private readonly ServerConfig _cfg;

        public ApiClient(ServerConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public string BaseUrl => _cfg.BaseUrl;

        public async UniTask<TResp> PostJsonAsync<TReq, TResp>(string path, TReq body, CancellationToken ct = default)
        {
            string url = _cfg.BaseUrl + path;
            string json = JsonConvert.SerializeObject(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req);
            req.timeout = (int)Mathf.Ceil(_cfg.RequestTimeoutSec);

            await req.SendWebRequest().WithCancellation(ct);
            return Handle<TResp>(req);
        }

        public async UniTask<TResp> GetAsync<TResp>(string path, CancellationToken ct = default)
        {
            string url = _cfg.BaseUrl + path;
            using var req = UnityWebRequest.Get(url);
            ApplyAuth(req);
            req.timeout = (int)Mathf.Ceil(_cfg.RequestTimeoutSec);
            await req.SendWebRequest().WithCancellation(ct);
            return Handle<TResp>(req);
        }

        public async UniTask PostJsonNoResponseAsync<TReq>(string path, TReq body, CancellationToken ct = default)
        {
            string url = _cfg.BaseUrl + path;
            string json = JsonConvert.SerializeObject(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            ApplyAuth(req);
            req.timeout = (int)Mathf.Ceil(_cfg.RequestTimeoutSec);

            await req.SendWebRequest().WithCancellation(ct);
            EnsureSuccess(req);
        }

        private void ApplyAuth(UnityWebRequest req)
        {
            var t = JwtStore.Token;
            if (!string.IsNullOrEmpty(t))
            {
                req.SetRequestHeader("Authorization", "Bearer " + t);
            }
            // ngrok 무료 플랜 경고 페이지 우회 (dev 전용 — 운영 서버엔 무해)
            req.SetRequestHeader("ngrok-skip-browser-warning", "1");
        }

        private TResp Handle<TResp>(UnityWebRequest req)
        {
            EnsureSuccess(req);
            string body = req.downloadHandler.text;
            if (string.IsNullOrEmpty(body)) return default;
            try
            {
                return JsonConvert.DeserializeObject<TResp>(body);
            }
            catch (Exception e)
            {
                throw new ApiException(req.responseCode, body, "json parse failed: " + e.Message);
            }
        }

        private void EnsureSuccess(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300)
            {
                return;
            }
            string body = req.downloadHandler != null ? req.downloadHandler.text : "";
            throw new ApiException(req.responseCode, body, $"{req.method} {req.url} failed: {req.error} body={body}");
        }
    }
}
