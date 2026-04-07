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

## FrogCatch prefab 자동 셋업 (Iter 2C')

`Assets/Editor/Bundles/SetupFrogCatchPrefab.cs` 가 Editor 시작 시 자동 실행되어:

1. `Assets/Minigames/Prefabs/` 디렉토리 생성
2. `FrogCatch.prefab` 이 없으면 새로 생성:
   - Root + FrogCatchGame
   - FrogSpawner child (frogPrefab 직렬화 할당)
   - Frog template child (SetActive=false)
3. AddressableAssetSettings 의 DefaultGroup 에 entry 등록 → address `minigame/frog_catch_v1`

수동 재실행: `ShortGeta → Bundles → Setup FrogCatch Prefab`

자동 셋업이 실패한 경우 (예: AddressableAssetSettings 가 아직 없을 때):
- Window → Asset Management → Addressables → Groups → "Create Addressables Settings"
- 그 후 메뉴로 수동 재실행

### Bootstrap 동작 분기

`BootstrapController` 의 `PlaySingleAsync(gameId)` 는 frog_catch_v1 에 대해:

1. `forceCodeFactoryForFrogCatch` 토글이 false 이면 → `Addressables.LoadAssetAsync<GameObject>("minigame/frog_catch_v1")` 시도
2. 실패하거나 토글이 true 이면 → 기존 코드 팩토리 fallback

Console 로그:
- 성공: `[Bundles] FrogCatch loaded from Addressables`
- 실패/fallback: `[Bundles] FrogCatch loaded from code factory (fallback)`

## Remote 호스팅 (Iter 2C'' 후속)

현재까지 (Iter 2C') 는 **로컬 (StreamingAssets / FastMode) 만** 다룬다. Iter 2C''
에서 다음을 추가:

1. AddressableAssetSettings → Profiles → 새 profile 'Remote'
2. RemoteBuildPath / RemoteLoadPath 를 `https://api.shortgeta.example/v1/bundles/[BuildTarget]` 형태로 설정
3. 그룹별로 Build/Load 경로를 Remote 로 변경
4. Build → New Build → 결과물을 서버 정적 디렉토리에 업로드
5. **서버 측**: `server/internal/bundles/handler.go` 에 `/v1/bundles/*` 라우트 추가, `static/` 디렉토리 서빙
6. **서버 DB**: `Game.bundle_url` 에 catalog URL + `bundle_hash` 채우기
7. **클라이언트**: 시작 시 `Addressables.LoadContentCatalogAsync(catalogUrl)` 로 catalog 갱신
8. **무결성**: bundle_hash 로 서버 응답과 다운로드 결과 비교

## 트러블슈팅

| 증상 | 해결 |
|---|---|
| `AddressableAssetSettings is null` | Window → Asset Management → Addressables → Groups → "Create Addressables Settings" |
| `InvalidKeyException: demo/hello` | 데모 자산을 Addressable 로 마크 안 함. 위 #2 단계 |
| Editor 에서만 동작, 빌드에서 실패 | Addressables Build 안 함. 위 #3 단계 |
| `Failed to download catalog from ...` | Remote profile 사용 중 + URL 잘못됨. profile 확인 |
