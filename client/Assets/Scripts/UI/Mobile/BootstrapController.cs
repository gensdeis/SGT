using Cysharp.Threading.Tasks;
using ShortGeta.Core;
using ShortGeta.Minigames.FrogCatch;
using ShortGeta.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShortGeta.UI.Mobile
{
    // 앱 진입점. 단일 GameObject 에 attach 하면 모든 흐름이 여기서 시작한다.
    //   1. ServerConfig 주입 받음 (Inspector)
    //   2. device id → 로그인 → JWT 저장 → TimeSync
    //   3. 게임 목록 로드 → "▶ 한판 더" 버튼 표시
    //   4. 클릭 → 세션 시작 → FrogCatch 1판 → 점수 제출 → Result 표시 → 홈
    //
    // UI 는 모두 런타임 programmatic 생성 (Iter 1 minimal). Iter 2 이후 prefab 화.
    public class BootstrapController : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;

        private ApiClient _api;
        private AuthApi _authApi;
        private GameApi _gameApi;
        private SessionApi _sessionApi;
        private RankingApi _rankingApi;
        private AnalyticsApi _analyticsApi;

        private Canvas _rootCanvas;
        private GameObject _homePanel;
        private GameObject _resultPanel;

        private GameView[] _games;

        private async void Start()
        {
            if (serverConfig == null)
            {
                Debug.LogError("[Bootstrap] ServerConfig 가 비어 있음. Inspector 슬롯 확인.");
                return;
            }
            _api = new ApiClient(serverConfig);
            _authApi = new AuthApi(_api);
            _gameApi = new GameApi(_api);
            _sessionApi = new SessionApi(_api);
            _rankingApi = new RankingApi(_api);
            _analyticsApi = new AnalyticsApi(_api);

            BuildRootUI();

            try
            {
                Debug.Log($"[Bootstrap] device id={JwtStore.DeviceId}");
                await _authApi.LoginByDeviceAsync(JwtStore.DeviceId);
                _games = await _gameApi.ListAsync();
                Debug.Log($"[Bootstrap] loaded {_games.Length} games");
                ShowHome();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] init failed: {e}");
                Toast.Show("서버 연결 실패: " + e.Message, 5f);
            }
        }

        private void BuildRootUI()
        {
            // Camera
            var camGo = new GameObject("[MainCamera]");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.07f, 0.1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.tag = "MainCamera";

            // EventSystem
            var es = new GameObject("[EventSystem]");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas
            var canvasGo = new GameObject("[RootCanvas]");
            _rootCanvas = canvasGo.AddComponent<Canvas>();
            _rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        private void ShowHome()
        {
            if (_resultPanel != null) Destroy(_resultPanel);
            if (_homePanel != null) Destroy(_homePanel);

            _homePanel = new GameObject("HomePanel");
            _homePanel.transform.SetParent(_rootCanvas.transform, false);
            var rt = _homePanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 타이틀
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_homePanel.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.85f);
            trt.anchorMax = new Vector2(1, 0.95f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "숏게타";
            titleText.fontSize = 80;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;

            // 게임 목록 텍스트
            var listGo = new GameObject("Games");
            listGo.transform.SetParent(_homePanel.transform, false);
            var lrt = listGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.1f, 0.35f);
            lrt.anchorMax = new Vector2(0.9f, 0.8f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var listText = listGo.AddComponent<TextMeshProUGUI>();
            listText.fontSize = 32;
            listText.alignment = TextAlignmentOptions.TopLeft;
            listText.color = new Color(0.85f, 0.85f, 0.85f);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("로드된 게임:");
            if (_games != null)
            {
                foreach (var g in _games)
                {
                    sb.AppendLine($"  • {g.Title} ({g.Id})");
                }
            }
            listText.text = sb.ToString();

            // "한판 더" 버튼
            var btnGo = new GameObject("PlayButton");
            btnGo.transform.SetParent(_homePanel.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.15f, 0.1f);
            brt.anchorMax = new Vector2(0.85f, 0.25f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(1f, 0.5f, 0.2f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var lrt2 = btnLabel.AddComponent<RectTransform>();
            lrt2.anchorMin = Vector2.zero;
            lrt2.anchorMax = Vector2.one;
            lrt2.offsetMin = Vector2.zero;
            lrt2.offsetMax = Vector2.zero;
            var btnText = btnLabel.AddComponent<TextMeshProUGUI>();
            btnText.text = "▶ 한판 더";
            btnText.fontSize = 64;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            btn.onClick.AddListener(() => StartSession().Forget());
        }

        private async UniTaskVoid StartSession()
        {
            try
            {
                _homePanel.SetActive(false);
                Debug.Log("[Bootstrap] starting session...");
                var session = await _sessionApi.StartAsync();
                Debug.Log($"[Bootstrap] session={session.SessionId} games={string.Join(",", session.GameIds)}");

                _analyticsApi.EventAsync("frog_catch_v1", "session_start", new { session_id = session.SessionId }).Forget();

                // Iter 1 단순화: session 의 추천 큐 무시하고 무조건 frog_catch 1판
                var result = await PlayFrogCatchAsync();
                _analyticsApi.EventAsync("frog_catch_v1", "game_end",
                    new { score = result.Score, play_time = result.PlayTimeSec }).Forget();

                // 점수 제출
                long ts = TimeSync.GetSyncedTimestamp();
                string sig = HmacSigner.Sign("frog_catch_v1", result.Score, result.PlayTimeSec,
                    ts, serverConfig.HmacBaseKey, serverConfig.BuildGuid);
                var endResp = await _sessionApi.EndAsync(session.SessionId, new[]
                {
                    new ScoreSubmission
                    {
                        GameId = "frog_catch_v1",
                        Score = result.Score,
                        PlayTime = result.PlayTimeSec,
                        Cleared = result.Score > 0,
                        Timestamp = ts,
                        Signature = sig,
                    },
                });
                Debug.Log($"[Bootstrap] end accepted={string.Join(",", endResp.Accepted)} rejected={string.Join(",", endResp.Rejected)}");

                ShowResult(result.Score, endResp);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] session failed: {e}");
                Toast.Show("세션 실패: " + e.Message, 4f);
                ShowHome();
            }
        }

        private async UniTask<MinigameResult> PlayFrogCatchAsync()
        {
            // FrogCatch GameObject + Spawner 동적 생성
            var gameGo = new GameObject("FrogCatchRuntime");
            var spawnerGo = new GameObject("FrogSpawner");
            spawnerGo.transform.SetParent(gameGo.transform, false);
            var spawner = spawnerGo.AddComponent<FrogSpawner>();

            // 개구리 prefab 이 없으므로 런타임에 간단한 sphere primitive 로 대체
            var primFrog = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            primFrog.name = "FrogPrefab";
            primFrog.transform.localScale = Vector3.one * 0.6f;
            var rend = primFrog.GetComponent<Renderer>();
            rend.material.color = new Color(0.2f, 0.8f, 0.3f);
            // Frog 컴포넌트 추가
            // SpriteRenderer 가 RequireComponent 로 강제되므로 dummy 추가
            primFrog.AddComponent<SpriteRenderer>();
            var frogComp = primFrog.AddComponent<Frog>();
            primFrog.SetActive(false);
            primFrog.transform.SetParent(gameGo.transform, false);
            // FrogSpawner 에 prefab 주입
            var sf = typeof(FrogSpawner).GetField("frogPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sf?.SetValue(spawner, frogComp);

            var game = gameGo.AddComponent<FrogCatchGame>();
            game.__TestSetSpawner(spawner);

            var launcher = gameGo.AddComponent<MinigameLauncher>();
            var tcs = new UniTaskCompletionSource<MinigameResult>();
            launcher.OnFinished += r => tcs.TrySetResult(r);

            // 짧은 카운트다운 표시 (Iter 1 minimal — toast 로 대체)
            for (int s = 3; s > 0; s--)
            {
                Toast.Show($"시작! {s}", 0.8f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(1));
            }

            launcher.Launch(game);
            var result = await tcs.Task;
            Destroy(gameGo);
            return result;
        }

        private void ShowResult(int score, EndSessionResponse end)
        {
            if (_homePanel != null) _homePanel.SetActive(false);
            if (_resultPanel != null) Destroy(_resultPanel);

            _resultPanel = new GameObject("ResultPanel");
            _resultPanel.transform.SetParent(_rootCanvas.transform, false);
            var rt = _resultPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = _resultPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_resultPanel.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.6f);
            trt.anchorMax = new Vector2(1, 0.85f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var t = titleGo.AddComponent<TextMeshProUGUI>();
            t.text = $"점수: {score}\n{(end.Accepted != null && end.Accepted.Length > 0 ? "✓ 서버 반영" : "✗ 거부됨")}";
            t.fontSize = 64;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;

            // 다시 버튼
            var btnGo = new GameObject("BackButton");
            btnGo.transform.SetParent(_resultPanel.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.2f, 0.15f);
            brt.anchorMax = new Vector2(0.8f, 0.28f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.3f, 0.6f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var lbl = labelGo.AddComponent<TextMeshProUGUI>();
            lbl.text = "홈으로";
            lbl.fontSize = 56;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;

            btn.onClick.AddListener(() =>
            {
                Destroy(_resultPanel);
                _resultPanel = null;
                ShowHome();
            });
        }
    }
}
