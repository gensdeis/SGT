# Addressables 빌드 절차 (Iter 2C MVP)

## 1회 셋업

### 1. AddressableAssetSettings 생성 (자동)

Unity 가 manifest.json 에서 `com.unity.addressables` 를 발견하고 import 하면
일부 버전은 자동으로 `Assets/AddressableAssetsData/` 를 생성한다. 자동 생성이
안 됐다면:

- 메뉴 **Window → Asset Management → Addressables → Groups**
- "Create Addressables Settings" 버튼 클릭

### 2. 데모 자산 마크

- Project 패널 → `Assets/Demo/HelloAddressable.txt` 클릭
- Inspector 상단 **Addressable** 체크박스 ✅
- 우측 텍스트 입력란 (기본값 = 자산 경로) 을 **`demo/hello`** 로 변경
- 다른 곳 클릭해서 변경 적용

### 3. (선택) Addressables Groups 빌드

- Window → Asset Management → Addressables → Groups
- 상단 **Build → New Build → Default Build Script**
- 빌드 결과: `Library/com.unity.addressables/aa/StandaloneWindows64/...`

> Editor 의 Play 모드에서는 빌드 없이도 동작 (FastMode). 실제 빌드된 Player 에서만
> bundle 이 필요.

## 동작 검증

Unity Editor ▶ Play 시 Console 에 다음 로그가 보여야 함:

```
[Bundles] AddressableBundleLoader.InitializeAsync OK
[Bundles] Hello from Addressable bundle!
```

두 번째 줄이 안 보이고 warning 만 나오면:
```
[Bundles] demo asset load failed (expected if not marked yet): ...
```
→ 위 #2 단계를 안 한 상태. Inspector 에서 Addressable 체크 + address 입력.

## 빌드 결과물 위치

| 플랫폼 | 경로 |
|---|---|
| Editor (FastMode) | bundle 빌드 불필요 — Editor 가 자산을 직접 로드 |
| Standalone Windows | `Library/com.unity.addressables/aa/StandaloneWindows64/StandaloneWindows64/` |
| Android | `Library/com.unity.addressables/aa/Android/Android/` |

빌드된 bundle 은 기본적으로 StreamingAssets 로 복사되어 Player 빌드에 포함된다.

## 미니게임 prefab 자동 셋업 (Iter 2C'')

`Assets/Editor/Bundles/SetupAllMinigamePrefabs.cs` 가 Editor 시작 시 자동 실행되어
6개 미니게임 모두를 prefab + Addressable 로 등록:

| Address | Prefab |
|---|---|
| `minigame/frog_catch_v1` | `Assets/Minigames/Prefabs/FrogCatch.prefab` |
| `minigame/noodle_boil_v1` | `Assets/Minigames/Prefabs/NoodleBoil.prefab` |
| `minigame/poker_face_v1` | `Assets/Minigames/Prefabs/PokerFace.prefab` |
| `minigame/dark_souls_v1` | `Assets/Minigames/Prefabs/DarkSouls.prefab` |
| `minigame/kakao_unread_v1` | `Assets/Minigames/Prefabs/KakaoUnread.prefab` |
| `minigame/math_genius_v1` | `Assets/Minigames/Prefabs/MathGenius.prefab` |

수동 재실행: `ShortGeta → Bundles → Setup All Minigame Prefabs`

### Bootstrap 동작 분기

`BootstrapController.PlaySingleAsync(gameId)`:
1. `forceCodeFactoryForAllGames` 토글이 false 이면 → `Addressables.LoadAssetAsync<GameObject>("minigame/{gameId}")` 시도
2. 실패하거나 토글이 true 이면 → 코드 팩토리 fallback

Console 로그:
- 성공: `[Bundles] {gameId} loaded from Addressables`
- 실패/fallback: `[Bundles] {gameId} loaded from code factory (fallback)`

## Iter 2C'''' 진행 상태

- ✅ **모든 unique catalog URL 로드** — `BootstrapController.TryLoadAllCatalogsAsync`
  - 게임별 `bundle_url` 을 HashSet 로 dedup 후 각각 `LoadCatalogAsync` 호출
- ✅ **bundle_hash 실 검증** — `Core/Bundles/BundleHashVerifier.cs`
  - SHA256 계산 → 서버 hex 와 비교
  - hash 가 비어있으면 skip (true 반환)
  - mismatch 시 catalog 로드 skip + warning
- ⏳ 서버 자동 hash 계산은 후속 (현재는 yaml 수동 입력)

## Iter 2C''' 진행 상태

- ✅ **서버 yaml 에 bundle_url 필드** (`server/internal/config/games.go` Game struct)
  - frog_catch_v1 에 데모용 `/v1/bundles/StandaloneWindows64/catalog.json` 설정
- ✅ **Service 가 yaml fallback** — DB 우선, 비어있으면 yaml
- ✅ **클라이언트 catalog 동적 로드** — `IBundleLoader.LoadCatalogAsync(url)`
  - `BootstrapController.TryLoadFirstCatalogAsync` 가 첫 번째 비어있지 않은
    `bundle_url` 로 `Addressables.LoadContentCatalogAsync` 호출
  - 상대 URL (`/v1/bundles/...`) 은 `ServerConfig.BaseUrl` 과 결합
- ⏳ bundle_hash 검증 — 로깅만, 실 비교는 후속

### 전체 흐름 (Iter 2C''' 부터)
```
1. 사용자가 Addressables Build → catalog.json + .bundle 파일들 생성
2. kubectl cp 결과물 → server pod /app/bundles/StandaloneWindows64/
3. 클라이언트가 시작 시:
   - GET /v1/games → bundle_url 받음
   - LoadContentCatalogAsync(http://localhost:18081/v1/bundles/StandaloneWindows64/catalog.json)
   - 이후 LoadAssetAsync("minigame/frog_catch_v1") 가 remote bundle 사용
```

## Iter 2C'' 진행 상태

- ✅ **6개 미니게임 prefab 자동 생성** (`SetupAllMinigamePrefabs.cs`)
- ✅ **BootstrapController 일반화**: `forceCodeFactoryForAllGames` 토글
- ✅ **서버 정적 라우트** `/v1/bundles/*` (Go `internal/bundles/handler.go`)
  - `BUNDLES_DIR` 환경변수 (기본 `./bundles`)
  - path traversal 방어 (`..` 차단 + `filepath.Abs` prefix 검사)
  - 캐시 헤더 immutable (1년)
- ⏳ Remote profile 셋업 + bundle 업로드는 사용자 작업

### 서버 측 디렉토리 준비

```bash
# kind 클러스터에서 bundles 디렉토리 만들기
kubectl -n shortgeta-dev exec deploy/shortgeta-server -- mkdir -p /app/bundles

# 서버 응답 확인
kubectl -n shortgeta-dev port-forward svc/shortgeta-server 18081:80
curl http://localhost:18081/v1/bundles/test.txt
# → 404 (디렉토리 비어있음, 라우트 정상)
```

## Remote 호스팅 — 사용자 작업

위 서버 라우트는 준비 완료. 다음 단계 (사용자):

1. AddressableAssetSettings → Profiles → 새 profile 'Remote' 만들기
2. RemoteBuildPath / RemoteLoadPath 를 `http://localhost:18081/v1/bundles/[BuildTarget]` 또는 실제 도메인으로 설정
3. 그룹별로 Build/Load 경로를 Remote 로 변경
4. Build → New Build → 결과물을 서버 pod 의 `/app/bundles/` 디렉토리에 복사 (`kubectl cp`)
5. 서버 DB의 `games.bundle_url` 에 catalog URL + `bundle_hash` 채우기 (Iter 2C''')
6. 클라이언트가 시작 시 `Addressables.LoadContentCatalogAsync(catalogUrl)` 호출 (Iter 2C''')

## 트러블슈팅

| 증상 | 해결 |
|---|---|
| `AddressableAssetSettings is null` | Window → Asset Management → Addressables → Groups → "Create Addressables Settings" |
| `InvalidKeyException: demo/hello` | 데모 자산을 Addressable 로 마크 안 함. 위 #2 단계 |
| Editor 에서만 동작, 빌드에서 실패 | Addressables Build 안 함. 위 #3 단계 |
| `Failed to download catalog from ...` | Remote profile 사용 중 + URL 잘못됨. profile 확인 |
