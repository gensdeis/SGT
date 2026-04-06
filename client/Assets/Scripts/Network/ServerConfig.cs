using UnityEngine;

namespace ShortGeta.Network
{
    // 서버 연결 설정 ScriptableObject.
    // dev/qa/real 별로 인스턴스를 만들어 BootstrapController 의 inspector 슬롯에 주입.
    [CreateAssetMenu(menuName = "ShortGeta/Server Config", fileName = "ServerConfig")]
    public class ServerConfig : ScriptableObject
    {
        [SerializeField] private string baseUrl = "http://localhost:18081";
        [SerializeField] private string buildGuid = "local-dev-build-guid";
        [SerializeField] private string hmacBaseKey = "local-dev-hmac-base-key";
        [SerializeField] private float requestTimeoutSec = 5f;

        public string BaseUrl => baseUrl;
        public string BuildGuid => buildGuid;
        public string HmacBaseKey => hmacBaseKey;
        public float RequestTimeoutSec => requestTimeoutSec;
    }
}
