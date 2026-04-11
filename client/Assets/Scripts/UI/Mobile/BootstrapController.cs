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
using ClassroomClick = ShortGeta.Minigames.ClassroomClick;
using TrackRun = ShortGeta.Minigames.TrackRun;
using PoleClimb = ShortGeta.Minigames.PoleClimb;
using FlyCatch = ShortGeta.Minigames.FlyCatch;
using SoccerTopdown = ShortGeta.Minigames.SoccerTopdown;
using SoccerSide = ShortGeta.Minigames.SoccerSide;
using DarkExplore = ShortGeta.Minigames.DarkExplore;
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
            // 신규 7종
            _registry.Register("classroom_click_v1", parent => parent.AddComponent<ClassroomClick.ClassroomClickGame>());
            _registry.Register("track_run_v1", parent => parent.AddComponent<TrackRun.TrackRunGame>());
            _registry.Register("pole_climb_v1", parent => parent.AddComponent<PoleClimb.PoleClimbGame>());
            _registry.Register("fly_catch_v1", parent => parent.AddComponent<FlyCatch.FlyCatchGame>());
            _registry.Register("soccer_topdown_v1", parent => parent.AddComponent<SoccerTopdown.SoccerTopdownGame>());
            _registry.Register("soccer_side_v1", parent => parent.AddComponent<SoccerSide.SoccerSideGame>());
            _registry.Register("dark_explore_v1", parent => parent.AddComponent<DarkExplore.DarkExploreGame>());
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

            // demo/hello 자산 로드는 Iter 2C MVP 흔적 — 제거.
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
            // spawner 필드 리플렉션 주입 (Editor-only __TestSetSpawner 대체, 빌드 호환)
            var spawnerField = typeof(FrogCatchGame).GetField("spawner",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            spawnerField?.SetValue(game, spawner);
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
            { "classroom_click_v1", "🖱" },
            { "track_run_v1", "🏃" },
            { "pole_climb_v1", "🏫" },
            { "fly_catch_v1", "🪰" },
            { "soccer_topdown_v1", "⚽" },
            { "soccer_side_v1", "⚽" },
            { "dark_explore_v1", "🔦" },
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> GameThumbBgHex = new()
        {
            { "frog_catch_v1",   "#0d3a2c" }, // 다크 그린 (개구리)
            { "noodle_boil_v1",  "#3a1d0d" }, // 다크 브라운 (라면)
            { "poker_face_v1",   "#1d1d3a" }, // 다크 인디고 (눈치)
            { "dark_souls_v1",   "#2a0d0d" }, // 다크 레드 (소울)
            { "kakao_unread_v1", "#3a3a0d" }, // 다크 옐로우 (카톡)
            { "math_genius_v1",  "#0d2a3a" }, // 다크 시안 (수학)
            { "classroom_click_v1", "#1a1e28" }, // 다크 교실
            { "track_run_v1",    "#0d2a0d" }, // 다크 잔디
            { "pole_climb_v1",   "#2a3040" }, // 다크 스카이
            { "fly_catch_v1",    "#3a3020" }, // 다크 모래
            { "soccer_topdown_v1", "#0d300d" }, // 다크 잔디
            { "soccer_side_v1",  "#203040" }, // 다크 하늘
            { "dark_explore_v1", "#0a0a10" }, // 거의 검정
        };

        // Iter UI v1.3: fake play count 는 /v1/me/game-stats 실 API 로 대체됨.

        // 하트 색상 상수
        private static readonly Color HeartWhite = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color HeartRed = new Color(1f, 0.28f, 0.37f, 1f); // #ff4760

        private void ShowHome()
        {
            if (_resultPanel != null) Destroy(_resultPanel);
            if (_homePanel != null) Destroy(_homePanel);

            // #6: game-stats 재로드 (플레이 횟수/최고 점수 갱신)
            RefreshGameStatsAsync().Forget();

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
            // 백 버튼 (iOS 용 UI, Android 도 겸용). 홈 탭이면 숨김.
            _backButton = BuildBackButton(topBar.transform);
            // "숏[게]타" — 가운데 글자 '게' 를 프로필 아바타 bg 와 동일한 AccentDark 로
            string accentHex = ColorUtility.ToHtmlStringRGB(DesignTokens.AccentDark);
            UIBuilder.Label(topBar.transform,
                $"숏<color=#{accentHex}>게</color>타", 56, DesignTokens.Text,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.03f, 0f), anchorMax: new Vector2(0.7f, 1f))
                .fontStyle = FontStyles.Bold;
            BuildProfileAvatar(topBar.transform);

            // 2) 검색바 (87~92%) — 라운드
            var search = UIBuilder.RoundedPanel(_homePanel.transform, "SearchBar",
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.92f),
                DesignTokens.Surface, 24);
            UIBuilder.Label(search.transform, "🔍  게임 검색",
                DesignTokens.FontBody, DesignTokens.TextDim,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.05f, 0f), anchorMax: new Vector2(0.95f, 1f));

            // 3) 콘텐츠 컨테이너 (10~86%) — 탭별 교체
            _tabContentContainer = UIBuilder.Panel(_homePanel.transform, "TabContent",
                new Vector2(0f, 0.10f), new Vector2(1f, 0.86f), DesignTokens.Bg);

            // 4) 하단 탭바 (0~10%)
            BuildBottomNav();

            // 초기 탭 = 홈
            SwitchTab(0);
        }

        // ─── 탭 관리 ───
        private GameObject _tabContentContainer;
        private int _activeTab = 0;
        private readonly System.Collections.Generic.List<(GameObject icon, GameObject label)> _navTabVisuals = new();
        private GameObject _backButton;
        private GameObject _quitPopup;
        private GameObject _pauseMenu;
        private bool _sessionActive;
        private GameObject _currentRuntimeGo;
        private UniTaskCompletionSource<MinigameResult> _currentMinigameTcs;

        private void SwitchTab(int idx)
        {
            _activeTab = idx;
            // 백 버튼: 홈 탭 외에서만 표시
            if (_backButton != null) _backButton.SetActive(idx != 0);
            // 콘텐츠 초기화
            if (_tabContentContainer != null)
            {
                foreach (Transform c in _tabContentContainer.transform) Destroy(c.gameObject);
                _cardThumbs.Clear();
            }
            // nav 하이라이트 갱신
            for (int i = 0; i < _navTabVisuals.Count; i++)
            {
                var (iconGo, labelGo) = _navTabVisuals[i];
                var color = (i == idx) ? DesignTokens.Accent : DesignTokens.TextDim;
                if (iconGo != null)
                {
                    var t = iconGo.GetComponent<TMPro.TextMeshProUGUI>();
                    if (t != null) t.color = color;
                }
                if (labelGo != null)
                {
                    var t = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
                    if (t != null) t.color = color;
                }
            }

            switch (idx)
            {
                case 0: BuildHomeTab(); break;
                case 1: BuildPopularTab(); break;
                case 2: BuildLibraryTab(); break;
                case 3: BuildSettingsTab(); break;
            }
        }

        // #6: 홈 진입 시 game-stats 재로드 (fire-and-forget)
        private async UniTaskVoid RefreshGameStatsAsync()
        {
            try
            {
                var statsResp = await _profileApi.GetGameStatsAsync();
                if (statsResp?.Stats != null)
                {
                    _gameStats.Clear();
                    foreach (var s in statsResp.Stats) _gameStats[s.GameId] = s;
                    Debug.Log($"[GameStats] refreshed {_gameStats.Count}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameStats] refresh failed: {e.Message}");
            }
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
            // 즉시 punch 애니메이션 — 서버 응답 기다리지 않고 반응감 확보
            PunchHeartAsync(heartLabel).Forget();
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
                heartLabel.text = "♥";
                heartLabel.color = res.Favorited ? HeartRed : HeartWhite;

                // 보관함 탭에서 unfavorite 하면 즉시 목록 갱신
                if (_activeTab == 2 && !res.Favorited)
                {
                    SwitchTab(2);
                }
            }
            catch (System.Exception e)
            {
                Toast.Show("보관함 실패: " + e.Message, 3f);
            }
        }

        // 하트 클릭 시 punch 효과 (1.0 → 1.5 → 1.0).
        private static async UniTaskVoid PunchHeartAsync(TMPro.TextMeshProUGUI heartLabel)
        {
            if (heartLabel == null) return;
            var rt = heartLabel.rectTransform;
            const float upDur = 0.10f;
            const float downDur = 0.18f;
            const float peak = 1.5f;
            float t = 0f;
            while (t < upDur)
            {
                if (heartLabel == null) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / upDur);
                // easeOutQuad
                k = 1f - (1f - k) * (1f - k);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, peak, k);
                await UniTask.Yield();
            }
            t = 0f;
            while (t < downDur)
            {
                if (heartLabel == null) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / downDur);
                // easeInOutCubic
                k = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
                rt.localScale = Vector3.one * Mathf.Lerp(peak, 1f, k);
                await UniTask.Yield();
            }
            if (heartLabel != null) rt.localScale = Vector3.one;
        }

        private void BuildProfileAvatar(Transform parent)
        {
            // 우상단 원형 아바타 — 고정 픽셀 크기로 정원 보장 (타원 왜곡 방지)
            const float avatarSize = 90f;
            var go = new GameObject("Profile");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            // anchor 는 한 점 (우측 중앙) — anchor box 가 0 이라 sizeDelta 가 실 픽셀
            rt.anchorMin = new Vector2(0.90f, 0.5f);
            rt.anchorMax = new Vector2(0.90f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(avatarSize, avatarSize);
            var img = go.AddComponent<Image>();
            img.color = DesignTokens.AccentDark;
            img.sprite = RoundedSpriteFactory.GetCircle();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            string initial = "?";
            if (_me != null && !string.IsNullOrEmpty(_me.Nickname))
                initial = _me.Nickname.Substring(0, 1);
            else if (_me != null)
                initial = "냉";
            UIBuilder.Label(go.transform, initial, 36, DesignTokens.PrimaryCTA,
                TextAlignmentOptions.Center)
                .fontStyle = FontStyles.Bold;

            // 코인 라벨 (아바타 왼쪽)
            int coins = _me?.Coins ?? 0;
            UIBuilder.Label(parent, $"🪙 {coins}", 32, DesignTokens.Gold,
                TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.55f, 0f), anchorMax: new Vector2(0.77f, 1f));
        }

        // ─── 백 네비게이션 (ESC / Android back / iOS UI 버튼) ───
        private GameObject BuildBackButton(Transform parent)
        {
            // 좌상단 원형 백버튼 — 고정 픽셀 크기 정원
            const float size = 72f;
            var go = new GameObject("BackBtn");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.5f);
            rt.anchorMax = new Vector2(0.08f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.color = DesignTokens.Surface;
            img.sprite = RoundedSpriteFactory.GetCircle();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            UIBuilder.Label(go.transform, "←", 42, DesignTokens.Text,
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(HandleBack);
            go.SetActive(false); // 초기엔 홈 탭이라 숨김
            return go;
        }

        private void Update()
        {
            // ESC (키보드/에디터) 또는 Android 뒤로가기 버튼
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBack();
            }
        }

        private void HandleBack()
        {
            // 1) 종료 팝업 열려있으면 닫기
            if (_quitPopup != null)
            {
                Destroy(_quitPopup);
                _quitPopup = null;
                return;
            }
            // 2) 일시정지 메뉴 열려있으면 재개
            if (_pauseMenu != null)
            {
                ResumeGame();
                return;
            }
            // 3) 세션 진행 중이면 일시정지 메뉴
            if (_sessionActive)
            {
                ShowPauseMenu();
                return;
            }
            // 4) 결과 화면이면 홈으로
            if (_resultPanel != null)
            {
                Destroy(_resultPanel);
                _resultPanel = null;
                ShowHome();
                return;
            }
            // 5) 홈 외 탭이면 홈 탭으로
            if (_activeTab != 0)
            {
                SwitchTab(0);
                return;
            }
            // 6) 홈 탭에서 한 번 더 → 종료 확인 팝업
            ShowQuitPopup();
        }

        private void ShowQuitPopup()
        {
            if (_quitPopup != null) return;
            // 반투명 오버레이
            _quitPopup = UIBuilder.Panel(_rootCanvas.transform, "QuitPopup",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.75f));
            // 오버레이 클릭 차단 (Button 없이 Image 만으로도 raycast 막힘)

            // 중앙 박스
            var box = UIBuilder.RoundedPanel(_quitPopup.transform, "Box",
                new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.62f),
                DesignTokens.Surface, 24);

            UIBuilder.Label(box.transform, "숏게타를 종료할까요?",
                DesignTokens.FontH2, DesignTokens.Text, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.05f, 0.55f), anchorMax: new Vector2(0.95f, 0.9f))
                .fontStyle = FontStyles.Bold;

            UIBuilder.Label(box.transform, "진행 중인 세션이 있으면 저장되지 않아요",
                DesignTokens.FontCaption, DesignTokens.TextDim, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.05f, 0.35f), anchorMax: new Vector2(0.95f, 0.55f));

            // 취소
            UIBuilder.Button(box.transform, "CancelBtn",
                DesignTokens.Surface2, DesignTokens.Text,
                "취소", DesignTokens.FontBody,
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.3f),
                () =>
                {
                    Destroy(_quitPopup);
                    _quitPopup = null;
                },
                radius: 20);

            // 종료
            UIBuilder.Button(box.transform, "QuitBtn",
                DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                "종료", DesignTokens.FontBody,
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.3f),
                () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                },
                radius: 20);
        }

        // ─── 일시정지 메뉴 (세션 진행 중) ───
        private void ShowPauseMenu()
        {
            if (_pauseMenu != null) return;
            // 게임 일시정지 — timeScale 0 + runtime GO 비활성화 (Input 폴링 차단)
            Time.timeScale = 0f;
            if (_currentRuntimeGo != null) _currentRuntimeGo.SetActive(false);

            // 최상위 보장을 위해 별도 Canvas 사용 (sortingOrder 9999)
            _pauseMenu = new GameObject("PauseMenuCanvas");
            _pauseMenu.transform.SetParent(_rootCanvas.transform.parent, false);
            var canvas = _pauseMenu.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            _pauseMenu.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode
                = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = _pauseMenu.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.matchWidthOrHeight = 0.5f;
            _pauseMenu.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            UIBuilder.FillPanel(_pauseMenu.transform, "Overlay",
                new Color(0f, 0f, 0f, 0.75f));

            var box = UIBuilder.RoundedPanel(_pauseMenu.transform, "Box",
                new Vector2(0.1f, 0.28f), new Vector2(0.9f, 0.72f),
                DesignTokens.Surface, 24);

            UIBuilder.Label(box.transform, "일시정지",
                DesignTokens.FontH2, DesignTokens.Text, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.05f, 0.82f), anchorMax: new Vector2(0.95f, 0.95f))
                .fontStyle = FontStyles.Bold;

            // 게임 재개 (최상단, PrimaryCTA)
            UIBuilder.Button(box.transform, "ResumeBtn",
                DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                "▶ 게임 재개", DesignTokens.FontBody,
                new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.75f),
                ResumeGame,
                radius: 20);

            // 설정
            UIBuilder.Button(box.transform, "SettingsBtn",
                DesignTokens.Surface2, DesignTokens.Text,
                "⚙ 설정", DesignTokens.FontBody,
                new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.52f),
                () => Toast.Show("세션 중 설정은 곧 나옵니다", 2f),
                radius: 20);

            // 그만하기 (하단)
            UIBuilder.Button(box.transform, "AbortBtn",
                DesignTokens.Surface2, DesignTokens.Hex("#f472b6"),
                "그만하기", DesignTokens.FontBody,
                new Vector2(0.08f, 0.09f), new Vector2(0.92f, 0.29f),
                AbortSession,
                radius: 20);
        }

        private void ResumeGame()
        {
            if (_pauseMenu != null)
            {
                Destroy(_pauseMenu);
                _pauseMenu = null;
            }
            Time.timeScale = 1f;
            if (_currentRuntimeGo != null) _currentRuntimeGo.SetActive(true);
        }

        private void AbortSession()
        {
            Time.timeScale = 1f;
            if (_pauseMenu != null)
            {
                Destroy(_pauseMenu);
                _pauseMenu = null;
            }
            // 현재 minigame 을 취소하면 StartSession 의 OperationCanceledException catch 가 ShowHome 호출
            _sessionActive = false;
            if (_currentMinigameTcs != null)
            {
                _currentMinigameTcs.TrySetException(new System.OperationCanceledException("user abort"));
            }
            if (_currentRuntimeGo != null)
            {
                Destroy(_currentRuntimeGo);
                _currentRuntimeGo = null;
            }
            _recording?.StopRecording();
        }

        // 탭 내부에 VerticalLayoutGroup + ContentSizeFitter 가 붙은 스크롤 컨테이너 생성.
        // 반환값은 자식을 추가할 content Transform.
        private Transform BuildScrollContainer(Transform parent)
        {
            var scroll = UIBuilder.Panel(parent, "Scroll",
                Vector2.zero, Vector2.one, DesignTokens.Bg);
            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            scroll.AddComponent<RectMask2D>();

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
            return content.transform;
        }

        // 섹션 헤더 (좌: 제목, 우: 부가 정보)
        private void BuildSectionHeader(Transform parent, string title, string trailing)
        {
            var go = new GameObject("SectionHeader");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 64;
            UIBuilder.Label(go.transform, title, DesignTokens.FontBody, DesignTokens.Text,
                TextAlignmentOptions.Left,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.7f, 1f))
                .fontStyle = FontStyles.Bold;
            if (!string.IsNullOrEmpty(trailing))
            {
                UIBuilder.Label(go.transform, trailing, DesignTokens.FontCaption, DesignTokens.TextDim,
                    TextAlignmentOptions.Right,
                    anchorMin: new Vector2(0.7f, 0f), anchorMax: new Vector2(1f, 1f));
            }
        }

        // ─── 홈 탭 ───
        // NEW 배지를 달 게임 id (목업과 동일 — poker_face_v1)
        private static readonly System.Collections.Generic.HashSet<string> NewGameIds = new()
        {
            "poker_face_v1"
        };

        private void BuildHomeTab()
        {
            var content = BuildScrollContainer(_tabContentContainer.transform);

            // 퀵 시작 카드
            BuildQuickStartCard(content);

            // "내 취향 게임" 섹션
            BuildSectionHeader(content, "내 취향 게임", $"{_games?.Length ?? 0}개");
            if (_games != null)
            {
                foreach (var g in _games)
                {
                    if (NewGameIds.Contains(g.Id)) continue; // NEW 는 아래 섹션에
                    BuildGameCard(content, g, rank: 0, showNew: false);
                }
            }

            // "새로 나왔어요" 섹션
            BuildSectionHeader(content, "새로 나왔어요", null);
            if (_games != null)
            {
                foreach (var g in _games)
                {
                    if (!NewGameIds.Contains(g.Id)) continue;
                    BuildGameCard(content, g, rank: 0, showNew: true);
                }
            }
        }

        // image.png 스펙 적용:
        // CardContainer (Rounded 24, #0B4635) + HorizontalLayoutGroup
        //   LeftContent (VerticalLayoutGroup) → TitleText + SubtitleText
        //   StartButton (Pill, #B2EBF2) → "▶ 바로 시작"
        private void BuildQuickStartCard(Transform parent)
        {
            var wrap = new GameObject("QuickStartWrap");
            wrap.transform.SetParent(parent, false);
            var wrapLe = wrap.AddComponent<LayoutElement>();
            wrapLe.preferredHeight = 180;

            // 카드 — 9-slice rounded Image + HLG
            var card = UIBuilder.RoundedPanel(wrap.transform, "AlgorithmSessionCard",
                Vector2.zero, Vector2.one,
                DesignTokens.Hex("#0B4635"), 24);
            var hlg = card.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(32, 24, 24, 24);
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false; // 버튼 세로 stretch 방지 (pill 모양 유지)

            // ── LeftContent (vertical title + subtitle) ──
            var left = new GameObject("LeftContent");
            left.transform.SetParent(card.transform, false);
            left.AddComponent<RectTransform>();
            var leftLe = left.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 1;
            leftLe.flexibleHeight = 1;
            leftLe.minHeight = 80;
            var vlg = left.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(left.transform, false);
            var titleLe = titleGo.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 50;
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "알고리즘 세션 시작";
            title.fontSize = 36;
            title.color = DesignTokens.Hex("#E7FFF6");
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Left;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24;
            title.fontSizeMax = 40;

            // Subtitle
            var subGo = new GameObject("SubtitleText");
            subGo.transform.SetParent(left.transform, false);
            var subLe = subGo.AddComponent<LayoutElement>();
            subLe.preferredHeight = 32;
            var sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.text = runFrogCatchOnly ? "디버그: frog_catch 1판" : "반응속도 · 동물 취향 맞춤";
            sub.fontSize = 22;
            sub.color = DesignTokens.Hex("#82C8AD");
            sub.alignment = TextAlignmentOptions.Left;
            sub.textWrappingMode = TextWrappingModes.NoWrap;

            // ── StartButton (pill) ──
            var btnGo = new GameObject("StartButton");
            btnGo.transform.SetParent(card.transform, false);
            var btnLe = btnGo.AddComponent<LayoutElement>();
            btnLe.preferredWidth = 220;
            btnLe.preferredHeight = 80;
            btnLe.flexibleWidth = 0;
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = DesignTokens.Hex("#B2EBF2");
            btnImg.sprite = RoundedSpriteFactory.GetRounded(40);
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => StartSession().Forget());
            var cb = btn.colors;
            cb.normalColor      = DesignTokens.Hex("#B2EBF2");
            cb.highlightedColor = DesignTokens.Hex("#9FE1CB");
            cb.pressedColor     = DesignTokens.Hex("#7ACBAA");
            cb.selectedColor    = DesignTokens.Hex("#B2EBF2");
            btn.colors = cb;

            // Button Label
            var btnLblGo = new GameObject("ButtonLabel");
            btnLblGo.transform.SetParent(btnGo.transform, false);
            var btnLblRt = btnLblGo.AddComponent<RectTransform>();
            btnLblRt.anchorMin = Vector2.zero;
            btnLblRt.anchorMax = Vector2.one;
            btnLblRt.offsetMin = Vector2.zero;
            btnLblRt.offsetMax = Vector2.zero;
            var btnLbl = btnLblGo.AddComponent<TextMeshProUGUI>();
            btnLbl.text = "▶ 바로 시작";
            btnLbl.fontSize = 28;
            btnLbl.color = DesignTokens.Hex("#04342C");
            btnLbl.fontStyle = FontStyles.Bold;
            btnLbl.alignment = TextAlignmentOptions.Center;
        }

        // ─── 인기 탭 ───
        private int _popularFilter = 0; // 0=지금 핫한, 1=역대 명작

        private void BuildPopularTab()
        {
            var content = BuildScrollContainer(_tabContentContainer.transform);

            // 필터 chip row
            var filterRow = new GameObject("FilterRow");
            filterRow.transform.SetParent(content, false);
            var le = filterRow.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            var hl = filterRow.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 12;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;

            BuildFilterChip(filterRow.transform, "지금 핫한", _popularFilter == 0, () => { _popularFilter = 0; SwitchTab(1); });
            BuildFilterChip(filterRow.transform, "역대 명작", _popularFilter == 1, () => { _popularFilter = 1; SwitchTab(1); });

            // 랭킹 카드 리스트 (play_count 기준 정렬)
            if (_games != null)
            {
                var sorted = new System.Collections.Generic.List<GameView>(_games);
                sorted.Sort((a, b) =>
                {
                    long ap = _gameStats.TryGetValue(a.Id, out var sa) ? sa.PlayCount : 0;
                    long bp = _gameStats.TryGetValue(b.Id, out var sb) ? sb.PlayCount : 0;
                    return bp.CompareTo(ap);
                });
                int rank = 1;
                foreach (var g in sorted)
                {
                    BuildGameCard(content, g, rank: rank++, showNew: false);
                }
            }
        }

        private void BuildFilterChip(Transform parent, string label, bool active, System.Action onClick)
        {
            var go = new GameObject($"Chip_{label}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 200;
            le.preferredHeight = 60;
            var img = go.AddComponent<Image>();
            img.color = active ? DesignTokens.AccentDark : DesignTokens.Surface;
            img.sprite = RoundedSpriteFactory.GetRounded(28);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var lt = UIBuilder.Label(go.transform, label, DesignTokens.FontCaption,
                active ? DesignTokens.PrimaryCTA : DesignTokens.TextDim,
                TextAlignmentOptions.Center);
            lt.fontStyle = FontStyles.Bold;
        }

        // ─── 보관함 탭 ───
        private void BuildLibraryTab()
        {
            var content = BuildScrollContainer(_tabContentContainer.transform);

            var favorites = new System.Collections.Generic.List<GameView>();
            if (_games != null)
            {
                foreach (var g in _games)
                {
                    if (_gameStats.TryGetValue(g.Id, out var s) && s.Favorited)
                        favorites.Add(g);
                }
            }

            foreach (var g in favorites)
            {
                BuildGameCard(content, g, rank: 0, showNew: false);
            }

            // placeholder 안내
            var hint = new GameObject("LibHint");
            hint.transform.SetParent(content, false);
            var le = hint.AddComponent<LayoutElement>();
            le.preferredHeight = 100;
            UIBuilder.Label(hint.transform,
                favorites.Count == 0 ? "♡ 하트 누르면 여기 저장돼요" : "♡ 하트 누르면 여기 저장돼요",
                DesignTokens.FontCaption, DesignTokens.TextDim,
                TextAlignmentOptions.Center);
        }

        // ─── 설정 탭 ───
        private void BuildSettingsTab()
        {
            var content = BuildScrollContainer(_tabContentContainer.transform);

            // 프로모션 카드 (광고 없이 즐기기)
            var promo = new GameObject("PromoWrap");
            promo.transform.SetParent(content, false);
            var pLe = promo.AddComponent<LayoutElement>();
            pLe.preferredHeight = 260;
            var pBg = UIBuilder.RoundedPanel(promo.transform, "Promo",
                Vector2.zero, Vector2.one, DesignTokens.QuickBg, 20);
            UIBuilder.Label(pBg.transform, "광고 없이 즐기기",
                DesignTokens.FontH2, DesignTokens.PrimaryCTA, TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.05f, 0.55f), anchorMax: new Vector2(0.95f, 0.92f))
                .fontStyle = FontStyles.Bold;
            UIBuilder.Label(pBg.transform, "모든 광고를 영구 제거하세요",
                DesignTokens.FontCaption, DesignTokens.PrimaryCTA, TextAlignmentOptions.TopLeft,
                anchorMin: new Vector2(0.05f, 0.35f), anchorMax: new Vector2(0.95f, 0.55f));
            UIBuilder.Button(pBg.transform, "PromoBtn",
                DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                "$2.99 한 번만", DesignTokens.FontCaption,
                new Vector2(0.05f, 0.08f), new Vector2(0.45f, 0.3f),
                () => Toast.Show("구매 플로우는 후속 iter", 2f),
                radius: 24);

            // 설정 리스트 그룹 1: 계정 / 랭킹
            BuildSettingsGroup(content, new (string icon, string title, string trailing, System.Action onClick)[]
            {
                ("👤", "계정 정보", "›", () => Toast.Show("계정 정보는 곧 나옵니다", 2f)),
                ("🏆", "내 랭킹 현황", "글로벌 #247", () => Toast.Show("랭킹 보기는 곧 나옵니다", 2f)),
            });

            // 그룹 2: 알림 / 그래픽 / 언어
            BuildSettingsGroup(content, new (string, string, string, System.Action)[]
            {
                ("🔔", "알림 설정", "›", () => Toast.Show("알림 설정은 곧 나옵니다", 2f)),
                ("🎨", "그래픽 설정", "›", () => Toast.Show("그래픽 설정은 곧 나옵니다", 2f)),
                ("🌐", "언어", "한국어", () => Toast.Show("언어 선택은 곧 나옵니다", 2f)),
            });

            // 그룹 3: 미션 (Iter 3 기능) / 버그제보 / 버전
            BuildSettingsGroup(content, new (string, string, string, System.Action)[]
            {
                ("📋", "오늘의 미션", "›", () => ShowMissionsAsync().Forget()),
                ("🐛", "버그 제보", "›", () => Toast.Show("버그 제보는 곧 나옵니다", 2f)),
                ("ℹ", "버전 정보", "v1.0.0", () => Toast.Show("숏게타 v1.0.0 dev", 3f)),
            });
        }

        private void BuildSettingsGroup(Transform parent, (string icon, string title, string trailing, System.Action onClick)[] items)
        {
            var groupGo = new GameObject("SettingsGroup");
            groupGo.transform.SetParent(parent, false);
            var le = groupGo.AddComponent<LayoutElement>();
            le.preferredHeight = items.Length * 100;

            var bg = UIBuilder.RoundedPanel(groupGo.transform, "GroupBg",
                Vector2.zero, Vector2.one, DesignTokens.Surface, 20);
            var vlg = bg.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 8, 8);
            vlg.spacing = 0;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var row = new GameObject($"Row_{i}");
                row.transform.SetParent(bg.transform, false);
                var rLe = row.AddComponent<LayoutElement>();
                rLe.preferredHeight = 88;
                var rowImg = row.AddComponent<Image>();
                rowImg.color = DesignTokens.Alpha(Color.white, 0f);
                var rowBtn = row.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                rowBtn.onClick.AddListener(() => item.onClick?.Invoke());

                UIBuilder.Label(row.transform, item.icon, 40, DesignTokens.Text,
                    TextAlignmentOptions.Left,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.15f, 1f));
                UIBuilder.Label(row.transform, item.title, DesignTokens.FontBody, DesignTokens.Text,
                    TextAlignmentOptions.Left,
                    anchorMin: new Vector2(0.17f, 0f), anchorMax: new Vector2(0.7f, 1f));
                UIBuilder.Label(row.transform, item.trailing, DesignTokens.FontCaption, DesignTokens.TextDim,
                    TextAlignmentOptions.Right,
                    anchorMin: new Vector2(0.7f, 0f), anchorMax: new Vector2(1f, 1f));

                // 구분선 (마지막 아이템 제외)
                if (i < items.Length - 1)
                {
                    var divider = new GameObject("Divider");
                    divider.transform.SetParent(bg.transform, false);
                    var dLe = divider.AddComponent<LayoutElement>();
                    dLe.preferredHeight = 2;
                    var dImg = divider.AddComponent<Image>();
                    dImg.color = DesignTokens.Border;
                }
            }
        }

        // 게임별 썸네일 Image 참조 캐시 — 후속에서 SetCardThumbnail(gameId, sprite) 로 교체 가능
        private readonly System.Collections.Generic.Dictionary<string, Image> _cardThumbs = new();

        // 외부에서 썸네일 sprite 를 동적으로 주입할 때 사용.
        public void SetCardThumbnail(string gameId, Sprite sprite)
        {
            if (_cardThumbs.TryGetValue(gameId, out var img) && img != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                // 이모지 라벨 숨김
                var label = img.transform.Find("Label");
                if (label != null) label.gameObject.SetActive(false);
            }
        }

        private void BuildGameCard(Transform parent, GameView g, int rank = 0, bool showNew = false)
        {
            // 카드 전체 라운드 + 클릭으로 개별 게임 바로 시작
            var card = UIBuilder.RoundedPanel(parent, $"Card_{g.Id}",
                Vector2.zero, Vector2.one, DesignTokens.Surface, 20);
            var le = card.AddComponent<LayoutElement>();
            le.preferredHeight = 480;

            // 카드 전체 버튼 (게임 시작)
            var cardBtn = card.AddComponent<Button>();
            cardBtn.targetGraphic = card.GetComponent<Image>();
            var cardColors = cardBtn.colors;
            cardColors.normalColor = DesignTokens.Surface;
            cardColors.highlightedColor = DesignTokens.Hex("#1e2030");
            cardColors.pressedColor = DesignTokens.Hex("#252838");
            cardBtn.colors = cardColors;
            string gameId = g.Id;
            cardBtn.onClick.AddListener(() => PlaySingleGameAsync(gameId).Forget());

            // 배지는 썸네일 생성 후에 추가 (밑에서 SetAsLastSibling 으로 최상위)

            // ─── 1. 썸네일 영역 (상단 68%, 게임별 색상, 라운드) ───
            var thumbColorHex = GameThumbBgHex.TryGetValue(g.Id, out var hex) ? hex : "#1b1e28";
            var thumb = UIBuilder.RoundedPanel(card.transform, "Thumb",
                new Vector2(0.02f, 0.34f), new Vector2(0.98f, 0.97f),
                DesignTokens.Hex(thumbColorHex), 16);
            _cardThumbs[g.Id] = thumb.GetComponent<Image>();

            string emoji = GameEmojis.TryGetValue(g.Id, out var e) ? e : "🎮";
            var emojiLabel = UIBuilder.Label(thumb.transform, emoji, 160, DesignTokens.Text,
                TextAlignmentOptions.Center);

            // 스프라이트 썸네일이 있으면 이모지 대체
            var thumbSprite = GameSpriteLoader.LoadThumbnail(g.Id);
            if (thumbSprite != null)
            {
                var sprGo = new GameObject("ThumbSprite");
                sprGo.transform.SetParent(thumb.transform, false);
                var srt = sprGo.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.05f, 0.05f);
                srt.anchorMax = new Vector2(0.95f, 0.95f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                var simg = sprGo.AddComponent<Image>();
                simg.sprite = thumbSprite;
                simg.preserveAspect = true; // 비율 유지, 영역 꽉 채움
                simg.raycastTarget = false;
                emojiLabel.gameObject.SetActive(false);
            }

            // 우하단 하트 — 썸네일 우측에 가까이 (경계에 붙지는 않음)
            bool favorited = _gameStats.TryGetValue(g.Id, out var stat0) && stat0.Favorited;
            var heart = UIBuilder.Panel(thumb.transform, "Heart",
                new Vector2(0.918f, 0.06f), new Vector2(1.023f, 0.20f),
                DesignTokens.Alpha(DesignTokens.Bg, 0f));
            var heartBtn = heart.AddComponent<Button>();
            heartBtn.targetGraphic = heart.GetComponent<Image>();
            var heartLabel = UIBuilder.Label(heart.transform, "♥", 48,
                favorited ? HeartRed : HeartWhite, TextAlignmentOptions.Center);
            heartBtn.onClick.AddListener(() => ToggleFavoriteAsync(g.Id, heartLabel).Forget());

            // 좌상단 랭킹 배지 (인기 탭) — 카드 최상위
            if (rank > 0)
            {
                var badge = UIBuilder.RoundedPanel(card.transform, "RankBadge",
                    new Vector2(0.03f, 0.88f), new Vector2(0.20f, 0.98f),
                    DesignTokens.Bg, 12);
                UIBuilder.Label(badge.transform, $"{rank}위",
                    DesignTokens.FontCaption, DesignTokens.Text,
                    TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                badge.transform.SetAsLastSibling();
            }

            // 우상단 NEW 배지 (홈 탭) — 카드 최상위
            if (showNew)
            {
                var newBadge = UIBuilder.RoundedPanel(card.transform, "NewBadge",
                    new Vector2(0.80f, 0.88f), new Vector2(0.97f, 0.98f),
                    DesignTokens.AccentDark, 12);
                UIBuilder.Label(newBadge.transform, "NEW",
                    DesignTokens.FontCaption, DesignTokens.PrimaryCTA,
                    TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
                newBadge.transform.SetAsLastSibling();
            }

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

            // 플레이 수 (실 서버 데이터)
            long playCount = _gameStats.TryGetValue(g.Id, out var stat) ? stat.PlayCount : 0;
            string plays = FormatCount(playCount);
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

            _navTabVisuals.Clear();
            BuildNavTab(nav.transform, "🏠", "홈",    0);
            BuildNavTab(nav.transform, "🔥", "인기",   1);
            BuildNavTab(nav.transform, "💝", "보관함", 2);
            BuildNavTab(nav.transform, "⚙", "설정",   3);
        }

        private void BuildNavTab(Transform parent, string icon, string label, int tabIdx)
        {
            var go = new GameObject($"Tab_{label}");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = DesignTokens.NavBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SwitchTab(tabIdx));

            var color = (tabIdx == _activeTab) ? DesignTokens.Accent : DesignTokens.TextDim;
            var iconLabel = UIBuilder.Label(go.transform, icon, 40, color, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0.4f), anchorMax: new Vector2(1f, 1f));
            var textLabel = UIBuilder.Label(go.transform, label, DesignTokens.FontTag, color, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0.4f));
            _navTabVisuals.Add((iconLabel.gameObject, textLabel.gameObject));
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
                _sessionActive = true;
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

                _sessionActive = false;
                ShowResult(results, endResp);
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Bootstrap] session aborted by user");
                _sessionActive = false;
                Time.timeScale = 1f;
                ShowHome();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] session failed: {e}");
                Toast.Show("세션 실패: " + e.Message, 4f);
                _sessionActive = false;
                Time.timeScale = 1f;
                ShowHome();
            }
        }

        // Phase 1: 카드 클릭 → 개별 게임 1판 (서버 세션 없이 로컬 플레이)
        private async UniTaskVoid PlaySingleGameAsync(string gameId)
        {
            if (!_registry.Contains(gameId))
            {
                Toast.Show($"게임 '{gameId}' 미등록", 2f);
                return;
            }
            try
            {
                _homePanel.SetActive(false);
                _sessionActive = true;
                _currentDdaIntensity = 0; // 로컬은 기본 난이도
                Debug.Log($"[Bootstrap] single game start: {gameId}");

                var result = await PlaySingleAsync(gameId);
                _sessionActive = false;

                // Phase 2: 미니 결과 → 다시하기 / 홈 (단일 게임은 "다음" 없음)
                while (true)
                {
                    var choice = await ShowMiniResultAsync(result, null);
                    if (choice == MiniResultChoice.Replay)
                    {
                        _sessionActive = true;
                        result = await PlaySingleAsync(gameId);
                        _sessionActive = false;
                        continue;
                    }
                    break; // Home or Next
                }
                ShowHome();
            }
            catch (System.OperationCanceledException)
            {
                Debug.Log("[Bootstrap] single game aborted");
                _sessionActive = false;
                Time.timeScale = 1f;
                ShowHome();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Bootstrap] single game failed: {e}");
                Toast.Show("게임 실패: " + e.Message, 3f);
                _sessionActive = false;
                Time.timeScale = 1f;
                ShowHome();
            }
        }

        private async UniTask<MinigameResult> PlaySingleAsync(string gameId)
        {
            var go = new GameObject($"Runtime.{gameId}");
            _currentRuntimeGo = go;

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
            _currentMinigameTcs = tcs;
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
            _currentRuntimeGo = null;
            _currentMinigameTcs = null;
            return result;
        }

        private async UniTask<List<MinigameResult>> PlayQueueAsync(string[] gameIds)
        {
            var results = new List<MinigameResult>(gameIds.Length);
            for (int i = 0; i < gameIds.Length;)
            {
                string id = gameIds[i];
                if (!_registry.Contains(id))
                {
                    Debug.LogWarning($"[Bootstrap] unregistered game id '{id}', skipping");
                    continue;
                }
                var r = await PlaySingleAsync(id);
                results.Add(r);

                // Phase 2: 각 게임 종료 후 미니 결과 표시 (마지막 게임 제외)
                if (i < gameIds.Length - 1)
                {
                    string nextId = null;
                    for (int j = i + 1; j < gameIds.Length; j++)
                    {
                        if (_registry.Contains(gameIds[j])) { nextId = gameIds[j]; break; }
                    }
                    var choice = await ShowMiniResultAsync(r, nextId);
                    if (choice == MiniResultChoice.Replay)
                    {
                        var replay = await PlaySingleAsync(id);
                        results[results.Count - 1] = replay; // 마지막 결과 교체
                        i--; // 같은 인덱스 다시 (다음 ++i 로 상쇄)
                    }
                    else if (choice == MiniResultChoice.Home)
                    {
                        break; // 세션 중단
                    }
                    // Next → 자동으로 다음 루프
                }
                i++;
            }
            return results;
        }

        // ─── Phase 2: 미니 결과 화면 ───
        private enum MiniResultChoice { Next, Replay, Home }
        private GameObject _miniResultPanel;

        private UniTask<MiniResultChoice> ShowMiniResultAsync(MinigameResult result, string nextGameId)
        {
            var tcs = new UniTaskCompletionSource<MiniResultChoice>();

            // 반투명 오버레이
            _miniResultPanel = UIBuilder.Panel(_rootCanvas.transform, "MiniResult",
                Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.85f));

            // 결과 카드
            var box = UIBuilder.RoundedPanel(_miniResultPanel.transform, "Box",
                new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.75f),
                DesignTokens.Surface, 24);

            // 게임 제목
            string emoji = GameEmojis.TryGetValue(result.GameId, out var em) ? em : "🎮";
            UIBuilder.Label(box.transform, $"{emoji}  {result.GameId}",
                DesignTokens.FontBody, DesignTokens.TextDim, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.05f, 0.82f), anchorMax: new Vector2(0.95f, 0.95f));

            // 점수 (큰 폰트)
            UIBuilder.Label(box.transform, $"{result.Score}",
                96, DesignTokens.Accent, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.1f, 0.55f), anchorMax: new Vector2(0.9f, 0.82f))
                .fontStyle = FontStyles.Bold;

            // 클리어 여부
            string clearText = result.Score > 0 ? "CLEAR!" : "FAILED";
            var clearColor = result.Score > 0 ? DesignTokens.PrimaryCTA : DesignTokens.Hex("#f472b6");
            UIBuilder.Label(box.transform, clearText,
                DesignTokens.FontH2, clearColor, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.1f, 0.42f), anchorMax: new Vector2(0.9f, 0.55f))
                .fontStyle = FontStyles.Bold;

            // 🔄 다시하기
            UIBuilder.Button(box.transform, "ReplayBtn",
                DesignTokens.AccentDark, DesignTokens.PrimaryCTA,
                "🔄 다시하기", DesignTokens.FontBody,
                new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.38f),
                () => { DestroyMiniResult(); tcs.TrySetResult(MiniResultChoice.Replay); },
                radius: 20);

            // ▶ 다음 게임
            if (nextGameId != null)
            {
                UIBuilder.Button(box.transform, "NextBtn",
                    DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                    "▶ 다음 게임", DesignTokens.FontBody,
                    new Vector2(0.08f, 0.06f), new Vector2(0.55f, 0.20f),
                    () => { DestroyMiniResult(); tcs.TrySetResult(MiniResultChoice.Next); },
                    radius: 20);
            }

            // 🏠 홈
            UIBuilder.Button(box.transform, "HomeBtn",
                DesignTokens.Surface2, DesignTokens.Text,
                "🏠 홈", DesignTokens.FontBody,
                new Vector2(nextGameId != null ? 0.58f : 0.08f, 0.06f),
                new Vector2(0.92f, 0.20f),
                () => { DestroyMiniResult(); tcs.TrySetResult(MiniResultChoice.Home); },
                radius: 20);

            // 스와이프 안내
            UIBuilder.Label(_miniResultPanel.transform, "↑ 위로 스와이프 = 다음 게임",
                DesignTokens.FontCaption, DesignTokens.TextDim, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0.1f, 0.05f), anchorMax: new Vector2(0.9f, 0.12f));

            // 스와이프 감지 (위로 스와이프 → 다음)
            var swipe = _miniResultPanel.AddComponent<MiniResultSwipeDetector>();
            swipe.OnSwipeUp = () => { DestroyMiniResult(); tcs.TrySetResult(MiniResultChoice.Next); };

            return tcs.Task;
        }

        private void DestroyMiniResult()
        {
            if (_miniResultPanel != null) { Destroy(_miniResultPanel); _miniResultPanel = null; }
        }

        // ─── 세션 결과 화면 (DesignTokens 통일) ───
        private void ShowResult(List<MinigameResult> results, EndSessionResponse end)
        {
            if (_homePanel != null) _homePanel.SetActive(false);
            if (_resultPanel != null) Destroy(_resultPanel);

            // 풀스크린 다크 배경
            _resultPanel = UIBuilder.Panel(_rootCanvas.transform, "ResultPanel",
                Vector2.zero, Vector2.one, DesignTokens.Bg);

            // 제목
            UIBuilder.Label(_resultPanel.transform, "세션 결과",
                DesignTokens.FontTitle, DesignTokens.Text, TextAlignmentOptions.Center,
                anchorMin: new Vector2(0f, 0.88f), anchorMax: new Vector2(1f, 0.97f))
                .fontStyle = FontStyles.Bold;

            // 총점 카드
            int total = 0;
            foreach (var r in results) total += r.Score;
            var totalCard = UIBuilder.RoundedPanel(_resultPanel.transform, "TotalCard",
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.87f),
                DesignTokens.Surface, 20);
            UIBuilder.Label(totalCard.transform, "합계",
                DesignTokens.FontCaption, DesignTokens.TextDim, TextAlignmentOptions.Left,
                anchorMin: new Vector2(0.08f, 0f), anchorMax: new Vector2(0.4f, 1f));
            UIBuilder.Label(totalCard.transform, $"{total:N0}",
                DesignTokens.FontH2, DesignTokens.Accent, TextAlignmentOptions.Right,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.92f, 1f))
                .fontStyle = FontStyles.Bold;

            // 게임별 점수 리스트 (스크롤)
            var scroll = UIBuilder.Panel(_resultPanel.transform, "ScoreScroll",
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.75f), DesignTokens.Bg);
            var sr = scroll.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            scroll.AddComponent<RectMask2D>();
            var content = new GameObject("Content");
            content.transform.SetParent(scroll.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 10;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            var acceptedSet = new HashSet<string>(end.Accepted ?? new string[0]);
            foreach (var r in results)
            {
                var row = UIBuilder.RoundedPanel(content.transform, $"Row_{r.GameId}",
                    Vector2.zero, Vector2.one, DesignTokens.Surface, 14);
                var rle = row.AddComponent<LayoutElement>();
                rle.preferredHeight = 100;

                string emoji = GameEmojis.TryGetValue(r.GameId, out var em) ? em : "🎮";
                bool accepted = acceptedSet.Contains(r.GameId);
                string mark = accepted ? "✓" : "✗";
                var markColor = accepted ? DesignTokens.Accent : DesignTokens.Hex("#f472b6");

                UIBuilder.Label(row.transform, mark, 40, markColor, TextAlignmentOptions.Center,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.1f, 1f))
                    .fontStyle = FontStyles.Bold;
                UIBuilder.Label(row.transform, $"{emoji} {r.GameId}",
                    DesignTokens.FontCaption, DesignTokens.Text, TextAlignmentOptions.Left,
                    anchorMin: new Vector2(0.12f, 0f), anchorMax: new Vector2(0.7f, 1f));
                UIBuilder.Label(row.transform, $"{r.Score}",
                    DesignTokens.FontBody, DesignTokens.Accent, TextAlignmentOptions.Right,
                    anchorMin: new Vector2(0.7f, 0f), anchorMax: new Vector2(0.95f, 1f))
                    .fontStyle = FontStyles.Bold;
            }

            // 하단 버튼들
            UIBuilder.Button(_resultPanel.transform, "RankBtn",
                DesignTokens.AccentDark, DesignTokens.PrimaryCTA,
                "🏆 랭킹 보기", DesignTokens.FontBody,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.26f),
                () => ShowRankingAsync(results).Forget(),
                radius: 20);

            if (_sessionHighlights.Count > 0)
            {
                UIBuilder.Button(_resultPanel.transform, "ShareBtn",
                    DesignTokens.Surface2, DesignTokens.Text,
                    "📤 공유", DesignTokens.FontCaption,
                    new Vector2(0.52f, 0.06f), new Vector2(0.92f, 0.14f),
                    () => { _recording?.ShareLastClip(); ClaimShareRewardAsync().Forget(); },
                    radius: 16);
            }

            UIBuilder.Button(_resultPanel.transform, "HomeBtn",
                DesignTokens.PrimaryCTA, DesignTokens.OnPrimary,
                "🏠 홈으로", DesignTokens.FontBody,
                new Vector2(0.08f, 0.06f), new Vector2(0.50f, 0.14f),
                () => { Destroy(_resultPanel); _resultPanel = null; ShowHome(); },
                radius: 20);
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
