using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ShortGeta.Core;
using ShortGeta.Core.Bundles;
using ShortGeta.Core.Recording;
using ShortGeta.Core.UI;
using ShortGeta.UI;
using ShortGeta.UI.PC;
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
        private System.Collections.Generic.Dictionary<string, GameStat> _gameStats = new();

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
                // 게임 stats (play_count / my_best / favorited) 동시 로드
                try
                {
                    var statsResp = await _profileApi.GetGameStatsAsync();
                    _gameStats.Clear();
                    if (statsResp?.Stats != null)
                    {
                        foreach (var s in statsResp.Stats) _gameStats[s.GameId] = s;
                    }
                    Debug.Log($"[GameStats] loaded {_gameStats.Count} entries");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[GameStats] load failed: {e.Message}");
                }
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

        // ─── Iter (UI v1.3): 게임별 이모지 + 썸네일 배경색 ───
        private static readonly System.Collections.Generic.Dictionary<string, string> GameEmojis = new()
        {
            { "frog_catch_v1", "🐸" },
            { "noodle_boil_v1", "🍜" },
            { "poker_face_v1", "🎭" },
            { "dark_souls_v1", "⚔" },
            { "kakao_unread_v1", "💬" },
            { "math_genius_v1", "🧮" },
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> GameThumbBgHex = new()
        {
            { "frog_catch_v1",   "#0d3a2c" }, // 다크 그린 (개구리)
            { "noodle_boil_v1",  "#3a1d0d" }, // 다크 브라운 (라면)
            { "poker_face_v1",   "#1d1d3a" }, // 다크 인디고 (눈치)
            { "dark_souls_v1",   "#2a0d0d" }, // 다크 레드 (소울)
            { "kakao_unread_v1", "#3a3a0d" }, // 다크 옐로우 (카톡)
            { "math_genius_v1",  "#0d2a3a" }, // 다크 시안 (수학)
        };

        // 가짜 플레이 수 (서버 API 전까지 placeholder)
        private static readonly System.Collections.Generic.Dictionary<string, string> FakePlayCount = new()
        {
            { "frog_catch_v1",   "3.2만" },
            { "noodle_boil_v1",  "2.8만" },
            { "poker_face_v1",   "1.9만" },
            { "dark_souls_v1",   "2.1만" },
            { "kakao_unread_v1", "1.7만" },
            { "math_genius_v1",  "1.4만" },
        };

        private void ShowHome()
        {
            if (_resultPanel != null) Destroy(_resultPanel);
            if (_homePanel != null) Destroy(_homePanel);

            // 플랫폼 분기 — PC 면 PcHomeController 위임
            if (PlatformDetector.Detect() == LayoutMode.PC)
            {
                _homePanel = new GameObject("PcHomeRoot");
                _homePanel.transform.SetParent(_rootCanvas.transform, false);
                var rt = _homePanel.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _homePanel.AddComponent<PcHomeController>().Build(_homePanel.transform);
                return;
            }

            // 모바일 루트 — 다크 배경
            _homePanel = UIBuilder.Panel(_rootCanvas.transform, "HomePanel",
                Vector2.zero, Vector2.one, DesignTokens.Bg);

            // 1) 상단바 (top 7%) — 로고 + 프로필 아바타
            var topBar = UIBuilder.Panel(_homePanel.transform, "TopBar",
                new Vector2(0f, 0.93f), new Vector2(1f, 1f), DesignTokens.Bg);
            UIBuilder.Label(topBar.transform, "숏게타", 56, DesignTokens.Text,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.05f, 0f), anchorMax: new Vector2(0.7f, 1f))
                .fontStyle = FontStyles.Bold;
            BuildProfileAvatar(topBar.transform);

            // 2) 검색바 (87~92%)
            var search = UIBuilder.Panel(_homePanel.transform, "SearchBar",
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.92f), DesignTokens.Surface);
            UIBuilder.Label(search.transform, "🔍  게임 검색",
                DesignTokens.FontBody, DesignTokens.TextDim,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.05f, 0f), anchorMax: new Vector2(0.95f, 1f));

            // 3) 퀵 시작 카드 (74~85%)
            var quick = UIBuilder.Panel(_homePanel.transform, "QuickStart",
                new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.85f), DesignTokens.QuickBg);
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

            // 4) 섹션 헤더
            var sectionHeader = UIBuilder.Panel(_homePanel.transform, "SectionHeader",
                new Vector2(0.05f, 0.69f), new Vector2(0.95f, 0.74f), DesignTokens.Bg);
            UIBuilder.Label(sectionHeader.transform, "내 취향 게임",
                DesignTokens.FontBody, DesignTokens.Text, TextAlignmentOptions.Left,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.7f, 1f))
                .fontStyle = FontStyles.Bold;
            UIBuilder.Label(sectionHeader.transform, $"{_games?.Length ?? 0}개",
                DesignTokens.FontCaption, DesignTokens.TextDim, TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.7f, 0f), anchorMax: new Vector2(1f, 1f));

            // 5) 게임 카드 ScrollView (10~69%)
            BuildGameList();

            // 5) 하단 탭바 (0~10%)
            BuildBottomNav();
        }

        private static string FormatCount(long n)
        {
            if (n >= 100_000_000) return $"{n / 100_000_000f:0.#}억";
            if (n >= 10_000) return $"{n / 10_000f:0.#}만";
            if (n >= 1_000) return $"{n / 1_000f:0.#}천";
            return n.ToString();
        }

        private async UniTaskVoid ToggleFavoriteAsync(string gameId, TMPro.TextMeshProUGUI heartLabel)
        {
            try
            {
                bool currently = _gameStats.TryGetValue(gameId, out var s) && s.Favorited;
                FavoriteResult res = currently
                    ? await _profileApi.RemoveFavoriteAsync(gameId)
                    : await _profileApi.AddFavoriteAsync(gameId);
                if (_gameStats.TryGetValue(gameId, out var existing))
                    existing.Favorited = res.Favorited;
                else
                    _gameStats[gameId] = new GameStat { GameId = gameId, Favorited = res.Favorited };
                heartLabel.text = res.Favorited ? "♥" : "♡";
            }
            catch (System.Exception e)
            {
                Toast.Show("보관함 실패: " + e.Message, 3f);
            }
        }

        private void BuildProfileAvatar(Transform parent)
        {
            // 우상단 원형 아바타 (닉네임 첫글자) + 코인 mini
            var go = UIBuilder.Panel(parent, "Profile",
                new Vector2(0.78f, 0.1f), new Vector2(0.95f, 0.9f),
                DesignTokens.AccentDark);
            string initial = "?";
            if (_me != null && !string.IsNullOrEmpty(_me.Nickname))
                initial = _me.Nickname.Substring(0, 1);
            else if (_me != null)
                initial = "냥";
            UIBuilder.Label(go.transform, initial, 36, DesignTokens.PrimaryCTA,
                TextAlignmentOptions.Center)
                .fontStyle = FontStyles.Bold;

            // 코인 라벨 (아바타 왼쪽)
            int coins = _me?.Coins ?? 0;
            UIBuilder.Label(parent, $"🪙 {coins}", 32, DesignTokens.Gold,
                TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.55f, 0f), anchorMax: new Vector2(0.77f, 1f));
        }

        private void BuildGameList()
        {
            // ScrollRect viewport
            var scroll = UIBuilder.Panel(_homePanel.transform, "GameScroll",
                new Vector2(0f, 0.10f), new Vector2(1f, 0.69f), DesignTokens.Bg);
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
            le.preferredHeight = 480;
            var bg = card.AddComponent<Image>();
            bg.color = DesignTokens.Surface;

            // ─── 1. 썸네일 영역 (상단 70%, 게임별 색상) ───
            var thumbColorHex = GameThumbBgHex.TryGetValue(g.Id, out var hex) ? hex : "#1b1e28";
            var thumb = UIBuilder.Panel(card.transform, "Thumb",
                new Vector2(0f, 0.32f), new Vector2(1f, 1f),
                DesignTokens.Hex(thumbColorHex));

            string emoji = GameEmojis.TryGetValue(g.Id, out var e) ? e : "🎮";
            UIBuilder.Label(thumb.transform, emoji, 160, DesignTokens.Text,
                TextAlignmentOptions.Center);

            // 우상단 하트 (보관함 토글 — 실 API 연동)
            bool favorited = _gameStats.TryGetValue(g.Id, out var stat0) && stat0.Favorited;
            var heart = UIBuilder.Panel(thumb.transform, "Heart",
                new Vector2(0.85f, 0.05f), new Vector2(0.97f, 0.20f),
                DesignTokens.Alpha(DesignTokens.Bg, 0f));
            var heartBtn = heart.AddComponent<Button>();
            heartBtn.targetGraphic = heart.GetComponent<Image>();
            var heartLabel = UIBuilder.Label(heart.transform, favorited ? "♥" : "♡", 56,
                DesignTokens.Hex("#f472b6"), TextAlignmentOptions.Center);
            heartBtn.onClick.AddListener(() => ToggleFavoriteAsync(g.Id, heartLabel).Forget());

            // ─── 2. 정보 영역 (하단 32%) ───
            // 제목
            UIBuilder.Label(card.transform, g.Title ?? g.Id,
                DesignTokens.FontBody, DesignTokens.Text, TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.04f, 0.20f), anchorMax: new Vector2(0.96f, 0.32f))
                .fontStyle = FontStyles.Bold;

            // 태그 + 메타 (한 줄)
            var metaRow = new GameObject("MetaRow");
            metaRow.transform.SetParent(card.transform, false);
            var mrt = metaRow.AddComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.04f, 0.04f);
            mrt.anchorMax = new Vector2(0.96f, 0.20f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            var hl = metaRow.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            if (g.Tags != null)
            {
                int count = 0;
                foreach (var t in g.Tags)
                {
                    if (count++ >= 2) break;
                    UIBuilder.Tag(metaRow.transform, t);
                }
            }

            // 플레이 수 (실 데이터 우선, 없으면 fake fallback)
            string plays;
            if (_gameStats.TryGetValue(g.Id, out var stat) && stat.PlayCount > 0)
                plays = FormatCount(stat.PlayCount);
            else
                plays = FakePlayCount.TryGetValue(g.Id, out var p) ? p : "—";
            var playLabel = new GameObject("Plays");
            playLabel.transform.SetParent(metaRow.transform, false);
            var plt = playLabel.AddComponent<TextMeshProUGUI>();
            plt.text = $"{plays} 플레이";
            plt.fontSize = DesignTokens.FontTag;
            plt.color = DesignTokens.TextDim;
            plt.alignment = TextAlignmentOptions.MidlineLeft;
            var plf = playLabel.AddComponent<ContentSizeFitter>();
            plf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 내 최고 점수 (Accent 색)
            var bestLabel = new GameObject("Best");
            bestLabel.transform.SetParent(metaRow.transform, false);
            var blt = bestLabel.AddComponent<TextMeshProUGUI>();
            int myBest = (_gameStats.TryGetValue(g.Id, out var st2)) ? st2.MyBest : 0;
            blt.text = myBest > 0 ? $"내 최고 {myBest:N0}" : "내 최고 —";
            blt.fontSize = DesignTokens.FontTag;
            blt.color = DesignTokens.Accent;
            blt.alignment = TextAlignmentOptions.MidlineLeft;
            blt.fontStyle = FontStyles.Bold;
            var blf = bestLabel.AddComponent<ContentSizeFitter>();
            blf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
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
