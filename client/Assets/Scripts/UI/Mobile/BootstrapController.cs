using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ShortGeta.Core;
using ShortGeta.Core.Bundles;
using ShortGeta.Core.Recording;
using ShortGeta.Core.UI;
// BundleHashVerifier 는 ShortGeta.Core.Bundles 네임스페이스에 있어 위 using 으로 충분
using ShortGeta.Minigames.DarkSouls;
using ShortGeta.Minigames.FrogCatch;
using ShortGeta.Minigames.KakaoUnread;
using ShortGeta.Minigames.MathGenius;
using ShortGeta.Minigames.NoodleBoil;
using ShortGeta.Minigames.PokerFace;
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
    //   4. 클릭 → 세션 시작 → MinigameSession 으로 추천 큐 순차 실행 → 일괄 점수 제출 → Result
    //
    // UI 는 모두 런타임 programmatic 생성. Iter 3 에서 prefab 화.
    public class BootstrapController : MonoBehaviour
    {
        [SerializeField] private ServerConfig serverConfig;
        [SerializeField] private bool runFrogCatchOnly = false; // true=Iter1 동작 (디버그용)
        [SerializeField] private bool forceCodeFactoryForAllGames = false; // true=Iter 2C'' Addressable 경로 skip 전체

        private ApiClient _api;
        private AuthApi _authApi;
        private GameApi _gameApi;
        private SessionApi _sessionApi;
        private RankingApi _rankingApi;
        private AnalyticsApi _analyticsApi;
        private ProfileApi _profileApi;
        private MissionsApi _missionsApi;
        private ShareApi _shareApi;
        private ProfileResponse _me;

        private MinigameRegistry _registry;
        private IRecordingService _recording;
        private IBundleLoader _bundleLoader;
        private BundleManifest _bundleManifest = new BundleManifest();
        private readonly List<SavedHighlight> _sessionHighlights = new List<SavedHighlight>();
        private int _currentDdaIntensity;

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
            _profileApi = new ProfileApi(_api);
            _missionsApi = new MissionsApi(_api);
            _shareApi = new ShareApi(_api);

            BuildRegistry();
            BuildRecordingService();
            BuildRootUI();

            try
            {
                Debug.Log($"[Bootstrap] device id={JwtStore.DeviceId}");

                // Bundle loader 초기화 + 데모 자산 로드 (Iter 2C MVP)
                await InitializeBundleLoaderAsync();

                await _authApi.LoginByDeviceAsync(JwtStore.DeviceId);
                // Iter 3: profile fetch (best-effort)
                try
                {
                    _me = await _profileApi.GetMeAsync();
                    Debug.Log($"[Profile] me loaded nick='{_me.Nickname}' coins={_me.Coins}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Profile] load failed: {e.Message}");
                }
                _games = await _gameApi.ListAsync();
                Debug.Log($"[Bootstrap] loaded {_games.Length} games");

                _bundleManifest.RegisterAll(_games);
                Debug.Log($"[Bundles] manifest registered {_bundleManifest.Count} entries (non-empty: {_bundleManifest.NonEmptyCount})");

                // Iter 2C'''': 모든 unique bundle URL 로 catalog 로드 + hash 검증
                await TryLoadAllCatalogsAsync();

                ShowHome();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] init failed: {e}");
                Toast.Show("서버 연결 실패: " + e.Message, 5f);
            }
        }

        // 6개 미니게임 모두 등록. 새 게임 추가 시 여기에 한 줄만 추가.
        private void BuildRegistry()
        {
            _registry = new MinigameRegistry();
            _registry.Register("frog_catch_v1", parent =>
            {
                // FrogCatch 는 별도 spawner + Frog primitive 가 필요해서 헬퍼 사용
                return CreateFrogCatch(parent);
            });
            _registry.Register("noodle_boil_v1", parent => parent.AddComponent<NoodleBoilGame>());
            _registry.Register("poker_face_v1", parent => parent.AddComponent<PokerFaceGame>());
            _registry.Register("dark_souls_v1", parent => parent.AddComponent<DarkSoulsGame>());
            _registry.Register("kakao_unread_v1", parent => parent.AddComponent<KakaoUnreadGame>());
            _registry.Register("math_genius_v1", parent => parent.AddComponent<MathGeniusGame>());
        }

        // Addressables 기반 IBundleLoader 초기화. 실패 시 Stub 으로 fallback.
        // 데모 자산 (demo/hello) 1개 로드 시도 — 사용자가 1회 마크해야 동작.
        private async UniTask InitializeBundleLoaderAsync()
        {
            try
            {
                _bundleLoader = new AddressableBundleLoader();
                await _bundleLoader.InitializeAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] Addressables init failed, using stub: {e.Message}");
                _bundleLoader = new StubBundleLoader();
            }

            // 데모 자산 로드 (없어도 무회귀)
            try
            {
                var hello = await _bundleLoader.LoadAssetAsync<TextAsset>("demo/hello");
                if (hello != null)
                {
                    Debug.Log($"[Bundles] {hello.text}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] demo asset load failed (expected if not marked yet): {e.Message}");
            }
        }

        // Iter 2C'''': 서버 GameView.bundle_url 의 모든 unique URL 에 대해
        // hash 검증 → 통과한 것만 LoadContentCatalogAsync. 같은 URL 중복 방지.
        private async UniTask TryLoadAllCatalogsAsync()
        {
            if (_bundleLoader == null || !_bundleLoader.IsReady)
            {
                Debug.LogWarning("[Bundles] LoadCatalogAsync skipped — loader not ready");
                return;
            }
            if (_games == null) return;

            var seen = new HashSet<string>();
            int loaded = 0;
            int failed = 0;

            foreach (var g in _games)
            {
                if (string.IsNullOrEmpty(g.BundleUrl)) continue;

                string fullUrl = g.BundleUrl;
                if (fullUrl.StartsWith("/"))
                {
                    fullUrl = serverConfig.BaseUrl.TrimEnd('/') + fullUrl;
                }
                if (!seen.Add(fullUrl)) continue; // 중복 skip

                bool hashOk = await BundleHashVerifier.VerifyAsync(fullUrl, g.BundleHash);
                if (!hashOk)
                {
                    Debug.LogWarning($"[Bundles] hash mismatch for {fullUrl} — skipping catalog");
                    failed++;
                    continue;
                }

                Debug.Log($"[Bundles] LoadCatalogAsync url={fullUrl}");
                await _bundleLoader.LoadCatalogAsync(fullUrl);
                loaded++;
            }

            Debug.Log($"[Bundles] catalogs: loaded={loaded} hash_failed={failed}");
            if (loaded == 0)
            {
                Debug.Log("[Bundles] no remote catalog loaded — using local Addressables");
            }
        }

        // 플랫폼별 RecordingService — Iter 2B' 부터 Android/iOS native bridge 활성.
        private void BuildRecordingService()
        {
            var go = new GameObject("[RecordingService]");
            go.transform.SetParent(transform, false);
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.LinuxPlayer:
                    _recording = go.AddComponent<EditorRecordingService>();
                    break;
                case RuntimePlatform.Android:
                    _recording = go.AddComponent<AndroidRecordingService>();
                    break;
                case RuntimePlatform.IPhonePlayer:
                    _recording = go.AddComponent<IosRecordingService>();
                    break;
                default:
                    _recording = go.AddComponent<NativeStubRecordingService>();
                    break;
            }
            Debug.Log($"[Bootstrap] recording service = {_recording.GetType().Name} (supported={_recording.IsSupported})");
        }

        // Iter 2C'': 임의 minigame address 'minigame/{gameId}' 로 Addressable 시도.
        // 성공 시 instance 의 IMinigame 반환, 실패/없음 시 null.
        private async UniTask<IMinigame> TryLoadMinigameFromAddressableAsync(string gameId, GameObject parent)
        {
            if (_bundleLoader == null || !_bundleLoader.IsReady) return null;
            string address = $"minigame/{gameId}";
            try
            {
                var prefab = await _bundleLoader.LoadAssetAsync<GameObject>(address);
                if (prefab == null) return null;
                var inst = Instantiate(prefab, parent.transform);
                inst.name = $"{gameId}(Addressable)";
                var game = inst.GetComponent<IMinigame>();
                if (game == null)
                {
                    Debug.LogWarning($"[Bundles] {address} loaded but no IMinigame component");
                    return null;
                }
                Debug.Log($"[Bundles] {gameId} loaded from Addressables");
                return game;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Bundles] {address} load failed: {e.Message}");
                return null;
            }
        }

        private IMinigame CreateFrogCatch(GameObject parent)
        {
            var spawnerGo = new GameObject("FrogSpawner");
            spawnerGo.transform.SetParent(parent.transform, false);
            var spawner = spawnerGo.AddComponent<FrogSpawner>();

            // Frog prefab 대용 sphere primitive
            var primFrog = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            primFrog.name = "FrogPrefab";
            primFrog.transform.localScale = Vector3.one * 0.6f;
            primFrog.GetComponent<Renderer>().material.color = new Color(0.2f, 0.8f, 0.3f);
            primFrog.AddComponent<SpriteRenderer>();
            var frogComp = primFrog.AddComponent<Frog>();
            primFrog.SetActive(false);
            primFrog.transform.SetParent(parent.transform, false);
            var sf = typeof(FrogSpawner).GetField("frogPrefab",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sf?.SetValue(spawner, frogComp);

            var game = parent.AddComponent<FrogCatchGame>();
            game.__TestSetSpawner(spawner);
            return game;
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

        // ─── Iter (UI v1.3): 게임별 이모지 매핑 ───
        private static readonly System.Collections.Generic.Dictionary<string, string> GameEmojis = new()
        {
            { "frog_catch_v1", "🐸" },
            { "noodle_boil_v1", "🍜" },
            { "poker_face_v1", "🎭" },
            { "dark_souls_v1", "⚔" },
            { "kakao_unread_v1", "💬" },
            { "math_genius_v1", "🧮" },
        };

        private void ShowHome()
        {
            if (_resultPanel != null) Destroy(_resultPanel);
            if (_homePanel != null) Destroy(_homePanel);

            // 루트 — 다크 배경
            _homePanel = UIBuilder.Panel(_rootCanvas.transform, "HomePanel",
                Vector2.zero, Vector2.one, DesignTokens.Bg);

            // 1) 상단바 (top 8%)
            var topBar = UIBuilder.Panel(_homePanel.transform, "TopBar",
                new Vector2(0f, 0.92f), new Vector2(1f, 1f), DesignTokens.NavBg);
            UIBuilder.Label(topBar.transform, "숏게타", 56, DesignTokens.Accent,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.05f, 0f), anchorMax: new Vector2(0.5f, 1f))
                .fontStyle = FontStyles.Bold;
            int coins = _me?.Coins ?? 0;
            UIBuilder.Label(topBar.transform, $"🪙 {coins}", 38, DesignTokens.Gold,
                TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.95f, 1f));

            // 2) 퀵 시작 카드 (78~90%)
            var quick = UIBuilder.Panel(_homePanel.transform, "QuickStart",
                new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.90f), DesignTokens.QuickBg);
            UIBuilder.Label(quick.transform, "알고리즘 세션", DesignTokens.FontH2, DesignTokens.PrimaryCTA,
                TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.05f, 0.4f), anchorMax: new Vector2(0.6f, 0.95f))
                .fontStyle = FontStyles.Bold;
            UIBuilder.Label(quick.transform,
                runFrogCatchOnly ? "디버그: frog_catch 1판" : $"취향 기반 추천 {_games?.Length ?? 0}판",
                DesignTokens.FontCaption, DesignTokens.PrimaryCTA,
                TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.05f, 0.05f), anchorMax: new Vector2(0.6f, 0.4f));
            UIBuilder.Button(quick.transform, "QuickPlayBtn",
                DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                "▶ 시작", DesignTokens.FontBody,
                new Vector2(0.62f, 0.2f), new Vector2(0.95f, 0.8f),
                () => StartSession().Forget());

            // 3) 섹션 헤더
            var sectionHeader = UIBuilder.Panel(_homePanel.transform, "SectionHeader",
                new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.78f), DesignTokens.Bg);
            UIBuilder.Label(sectionHeader.transform, "내 취향 게임",
                DesignTokens.FontBody, DesignTokens.Text, TextAlignmentOptions.Left,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.7f, 1f))
                .fontStyle = FontStyles.Bold;
            UIBuilder.Label(sectionHeader.transform, $"{_games?.Length ?? 0}개",
                DesignTokens.FontCaption, DesignTokens.TextDim, TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.7f, 0f), anchorMax: new Vector2(1f, 1f));

            // 4) 게임 카드 ScrollView (12~73%)
            BuildGameList();

            // 5) 하단 탭바 (0~10%)
            BuildBottomNav();
        }

        private void BuildGameList()
        {
            // ScrollRect viewport
            var scroll = UIBuilder.Panel(_homePanel.transform, "GameScroll",
                new Vector2(0f, 0.10f), new Vector2(1f, 0.73f), DesignTokens.Bg);
            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            var mask = scroll.AddComponent<RectMask2D>();

            var content = new GameObject("Content");
            content.transform.SetParent(scroll.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(20, 0);
            crt.offsetMax = new Vector2(-20, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 16;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            if (_games != null)
            {
                foreach (var g in _games)
                {
                    BuildGameCard(content.transform, g);
                }
            }
        }

        private void BuildGameCard(Transform parent, GameView g)
        {
            var card = new GameObject($"Card_{g.Id}");
            card.transform.SetParent(parent, false);
            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 180;
            var img = card.AddComponent<Image>();
            img.color = DesignTokens.Surface;

            // 좌측 이모지
            string emoji = GameEmojis.TryGetValue(g.Id, out var e) ? e : "🎮";
            UIBuilder.Label(card.transform, emoji, 96, DesignTokens.Text,
                TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.25f, 1f));

            // 우측 정보
            UIBuilder.Label(card.transform, g.Title ?? g.Id,
                DesignTokens.FontBody, DesignTokens.Text, TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.27f, 0.55f), anchorMax: new Vector2(0.98f, 0.95f))
                .fontStyle = FontStyles.Bold;

            // 태그 (Horizontal layout)
            var tagsGo = new GameObject("Tags");
            tagsGo.transform.SetParent(card.transform, false);
            var trt = tagsGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.27f, 0.25f);
            trt.anchorMax = new Vector2(0.98f, 0.55f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var hl = tagsGo.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            if (g.Tags != null)
            {
                int count = 0;
                foreach (var t in g.Tags)
                {
                    if (count++ >= 3) break;
                    UIBuilder.Tag(tagsGo.transform, t);
                }
            }

            // 하단 메타
            UIBuilder.Label(card.transform, $"id: {g.Id}",
                DesignTokens.FontTag, DesignTokens.TextDim, TextAlignmentOptions.BottomLeft,
                anchorMin: new Vector2(0.27f, 0.05f), anchorMax: new Vector2(0.98f, 0.25f));
        }

        private void BuildBottomNav()
        {
            var nav = UIBuilder.Panel(_homePanel.transform, "BottomNav",
                new Vector2(0f, 0f), new Vector2(1f, 0.10f), DesignTokens.NavBg);
            var hl = nav.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            hl.padding = new RectOffset(0, 0, 8, 8);

            BuildNavTab(nav.transform, "🏠", "홈", true, null);
            BuildNavTab(nav.transform, "🔥", "인기", false, () => Toast.Show("인기 탭은 곧 나옵니다", 2f));
            BuildNavTab(nav.transform, "💝", "보관함", false, () => Toast.Show("보관함은 곧 나옵니다", 2f));
            BuildNavTab(nav.transform, "📋", "미션", false, () => ShowMissionsAsync().Forget());
        }

        private void BuildNavTab(Transform parent, string icon, string label, bool active, System.Action onClick)
        {
            var go = new GameObject($"Tab_{label}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = DesignTokens.NavBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var color = active ? DesignTokens.Accent : DesignTokens.TextDim;
            UIBuilder.Label(go.transform, icon, 40, color, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0.4f), anchorMax: new Vector2(1f, 1f));
            UIBuilder.Label(go.transform, label, DesignTokens.FontTag, color, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0.4f));
        }

        // Iter 3: 미션 리스트 + 첫 claimable 자동 claim
        private async UniTaskVoid ShowMissionsAsync()
        {
            try
            {
                var resp = await _missionsApi.TodayAsync();
                Debug.Log($"[Missions] {resp.Missions?.Length ?? 0} today");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("【오늘의 미션】");
                if (resp.Missions != null)
                {
                    foreach (var m in resp.Missions)
                    {
                        string state = m.Claimed ? "✅" : (m.Completed ? "🎁" : $"{m.Progress}/{m.Target}");
                        sb.AppendLine($"{state} {m.Title} (+{m.Reward})");
                    }
                }
                Toast.Show(sb.ToString(), 5f);

                // 첫 claimable 자동 claim
                if (resp.Missions != null)
                {
                    foreach (var m in resp.Missions)
                    {
                        if (m.Completed && !m.Claimed)
                        {
                            var cr = await _missionsApi.ClaimAsync(m.MissionId);
                            if (cr.Ok)
                            {
                                Toast.Show($"미션 보상 +{cr.Reward}🪙 (총 {cr.Coins})", 4f);
                                if (_me != null) _me.Coins = cr.Coins;
                            }
                            break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Toast.Show("미션 로드 실패: " + e.Message, 4f);
            }
        }

        private async UniTaskVoid StartSession()
        {
            try
            {
                _homePanel.SetActive(false);
                _sessionHighlights.Clear();
                Debug.Log("[Bootstrap] starting session...");
                var session = await _sessionApi.StartAsync();
                _currentDdaIntensity = session.DdaIntensity;
                Debug.Log($"[Bootstrap] session={session.SessionId} games={string.Join(",", session.GameIds)} dda_intensity={_currentDdaIntensity}");

                _analyticsApi.EventAsync("session", "start", new { session_id = session.SessionId }).Forget();

                List<MinigameResult> results;
                if (runFrogCatchOnly)
                {
                    // 디버그: frog_catch 1판
                    results = new List<MinigameResult> { await PlaySingleAsync("frog_catch_v1") };
                }
                else
                {
                    // 풀 세션: 추천 큐 그대로
                    results = await PlayQueueAsync(session.GameIds);
                }

                // 점수 제출
                var subs = new List<ScoreSubmission>(results.Count);
                long now = TimeSync.GetSyncedTimestamp();
                foreach (var r in results)
                {
                    long ts = now;
                    string sig = HmacSigner.Sign(r.GameId, r.Score, r.PlayTimeSec, ts,
                        serverConfig.HmacBaseKey, serverConfig.BuildGuid);
                    subs.Add(new ScoreSubmission
                    {
                        GameId = r.GameId,
                        Score = r.Score,
                        PlayTime = r.PlayTimeSec,
                        Cleared = r.Score > 0,
                        Timestamp = ts,
                        Signature = sig,
                    });
                }
                var endResp = await _sessionApi.EndAsync(session.SessionId, subs.ToArray());
                Debug.Log($"[Bootstrap] end accepted={string.Join(",", endResp.Accepted)} rejected={string.Join(",", endResp.Rejected)}");

                ShowResult(results, endResp);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] session failed: {e}");
                Toast.Show("세션 실패: " + e.Message, 4f);
                ShowHome();
            }
        }

        private async UniTask<MinigameResult> PlaySingleAsync(string gameId)
        {
            var go = new GameObject($"Runtime.{gameId}");

            // FrogCatch 만 Addressable 우선 시도 (Iter 2C')
            IMinigame game = null;
            if (!forceCodeFactoryForAllGames)
            {
                game = await TryLoadMinigameFromAddressableAsync(gameId, go);
            }
            if (game == null)
            {
                game = _registry.Create(gameId, go);
                Debug.Log($"[Bundles] {gameId} loaded from code factory (fallback)");
            }

            // DDA 강도 적용 (옵션 인터페이스, OnGameStart 이전)
            if (game is IDifficultyAware diffAware)
            {
                diffAware.SetDifficulty(_currentDdaIntensity);
                Debug.Log($"[DDA] {gameId} difficulty={_currentDdaIntensity}");
            }

            var launcher = go.AddComponent<MinigameLauncher>();
            var tcs = new UniTaskCompletionSource<MinigameResult>();
            launcher.OnFinished += r => tcs.TrySetResult(r);

            // 카운트다운
            for (int s = 3; s > 0; s--)
            {
                Toast.Show($"{game.Title} 시작! {s}", 0.8f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(1));
            }

            // 녹화 시작
            string tag = $"{gameId}-{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            _recording?.StartRecording(tag);

            launcher.Launch(game);
            var result = await tcs.Task;

            // 녹화 종료 + 저장
            _recording?.StopRecording();
            var clip = _recording?.FlushLastClip();
            if (clip.HasValue)
            {
                _sessionHighlights.Add(clip.Value);
            }

            Destroy(go);
            return result;
        }

        private async UniTask<List<MinigameResult>> PlayQueueAsync(string[] gameIds)
        {
            var results = new List<MinigameResult>(gameIds.Length);
            foreach (var id in gameIds)
            {
                if (!_registry.Contains(id))
                {
                    Debug.LogWarning($"[Bootstrap] unregistered game id '{id}', skipping");
                    continue;
                }
                var r = await PlaySingleAsync(id);
                results.Add(r);
            }
            return results;
        }

        private void ShowResult(List<MinigameResult> results, EndSessionResponse end)
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
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_resultPanel.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.85f);
            trt.anchorMax = new Vector2(1, 0.95f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var t = titleGo.AddComponent<TextMeshProUGUI>();
            t.text = "결과";
            t.fontSize = 64;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;

            var listGo = new GameObject("ScoreList");
            listGo.transform.SetParent(_resultPanel.transform, false);
            var lrt = listGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.1f, 0.3f);
            lrt.anchorMax = new Vector2(0.9f, 0.8f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var lt = listGo.AddComponent<TextMeshProUGUI>();
            lt.fontSize = 40;
            lt.alignment = TextAlignmentOptions.TopLeft;
            lt.color = Color.white;

            var sb = new System.Text.StringBuilder();
            string ddaLabel = _currentDdaIntensity == 1 ? "+1 어려움"
                            : _currentDdaIntensity == -1 ? "-1 쉬움"
                            : "0 기본";
            sb.AppendLine($"DDA: {ddaLabel}");
            sb.AppendLine();
            int total = 0;
            var acceptedSet = new HashSet<string>(end.Accepted ?? new string[0]);
            foreach (var r in results)
            {
                string mark = acceptedSet.Contains(r.GameId) ? "✓" : "✗";
                sb.AppendLine($"{mark} {r.GameId,-20} {r.Score,5}");
                total += r.Score;
            }
            sb.AppendLine();
            sb.AppendLine($"합계: {total}");
            lt.text = sb.ToString();

            // 하이라이트 보기 + 공유 버튼 (highlight 가 있을 때만)
            if (_sessionHighlights.Count > 0)
            {
                CreateRectButton("HighlightButton", new Vector2(0.05f, 0.23f), new Vector2(0.48f, 0.32f),
                    new Color(1f, 0.6f, 0.2f), $"📁 보기 ({_sessionHighlights.Count})", 38,
                    () =>
                    {
                        if (_recording != null && _recording.IsSupported)
                            _recording.OpenLastClipExternally();
                        else
                            Toast.Show("Editor / Standalone 에서만 폴더 열기 가능", 3f);
                    });

                CreateRectButton("ShareButton", new Vector2(0.52f, 0.23f), new Vector2(0.95f, 0.32f),
                    new Color(0.2f, 0.7f, 0.4f), "📤 공유", 38,
                    () =>
                    {
                        if (_recording != null) _recording.ShareLastClip();
                        else Toast.Show("녹화 서비스 없음", 3f);
                        // Iter 3: 공유 보상 claim (서버 일 1회 limit)
                        ClaimShareRewardAsync().Forget();
                    });
            }

            // Iter 3: 랭킹 보기 버튼 (홈으로 위)
            CreateRectButton("RankingButton", new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.42f),
                new Color(0.4f, 0.5f, 0.9f), "🏆 랭킹 보기", 38,
                () => ShowRankingAsync(results).Forget());

            var btnGo = new GameObject("BackButton");
            btnGo.transform.SetParent(_resultPanel.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.2f, 0.1f);
            brt.anchorMax = new Vector2(0.8f, 0.2f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.3f, 0.6f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var llrt = labelGo.AddComponent<RectTransform>();
            llrt.anchorMin = Vector2.zero;
            llrt.anchorMax = Vector2.one;
            llrt.offsetMin = Vector2.zero;
            llrt.offsetMax = Vector2.zero;
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

        // Iter 3: 공유 보상 claim
        private async UniTaskVoid ClaimShareRewardAsync()
        {
            try
            {
                var cr = await _shareApi.ClaimAsync("unknown", "session");
                if (cr.Ok)
                {
                    Toast.Show($"공유 보상 +{cr.Reward}🪙 (총 {cr.Coins})", 4f);
                    if (_me != null) _me.Coins = cr.Coins;
                }
                else
                {
                    Toast.Show("오늘 공유 보상은 이미 받았어요", 3f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Share] claim failed: {e.Message}");
            }
        }

        // Iter 3: 첫 게임 기준 top 10 표시
        private async UniTaskVoid ShowRankingAsync(List<MinigameResult> results)
        {
            try
            {
                string gameId = (results != null && results.Count > 0) ? results[0].GameId : "frog_catch_v1";
                var resp = await _rankingApi.ByGameAsync(gameId, 10);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"【{gameId} TOP 10】");
                if (resp.Rankings != null)
                {
                    foreach (var r in resp.Rankings)
                    {
                        string uid = r.UserId.Length > 8 ? r.UserId.Substring(0, 8) : r.UserId;
                        sb.AppendLine($"{r.Rank,2}. {uid}  {r.BestScore}");
                    }
                }
                Toast.Show(sb.ToString(), 6f);
            }
            catch (System.Exception e)
            {
                Toast.Show("랭킹 로드 실패: " + e.Message, 3f);
            }
        }

        // 작은 helper — Result UI 에 평면 버튼 추가
        private void CreateRectButton(string name, Vector2 anchorMin, Vector2 anchorMax,
            Color color, string label, int fontSize, System.Action onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(_resultPanel.transform, false);
            var rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = btnGo.AddComponent<Image>();
            img.color = color;
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
            lbl.text = label;
            lbl.fontSize = fontSize;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.color = Color.white;

            btn.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
