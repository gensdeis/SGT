using UnityEngine;

namespace ShortGeta.Network
{
    // JWT 토큰을 PlayerPrefs 에 저장 + 조회.
    // 보안: PlayerPrefs 는 평문 저장이므로 mobile 에서 권장 X.
    // Iter 2 에서 Android Keystore / iOS Keychain 으로 교체 예정.
    public static class JwtStore
    {
        private const string Key = "shortgeta.jwt";
        private const string DeviceIdKey = "shortgeta.device_id";

        public static string Token
        {
            get => PlayerPrefs.GetString(Key, "");
            set
            {
                PlayerPrefs.SetString(Key, value ?? "");
                PlayerPrefs.Save();
            }
        }

        public static string DeviceId
        {
            get
            {
                string id = PlayerPrefs.GetString(DeviceIdKey, "");
                if (string.IsNullOrEmpty(id))
                {
                    // SystemInfo.deviceUniqueIdentifier 는 일부 기기에서 변경됨.
                    // GUID 생성 + PlayerPrefs 저장이 더 안정적.
                    id = System.Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(DeviceIdKey, id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        public static bool HasToken => !string.IsNullOrEmpty(Token);

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
