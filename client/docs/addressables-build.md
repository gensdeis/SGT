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

## Remote 호스팅 (Iter 2C')

현재 Iter 2C MVP 는 **로컬 (StreamingAssets) 만** 다룬다. Iter 2C' 에서 다음을 추가:

1. AddressableAssetSettings → Profiles → 새 profile 'Remote'
2. RemoteBuildPath 와 RemoteLoadPath 를 GHCR 또는 별도 HTTP CDN URL 로 설정
3. 그룹별로 Build/Load 경로를 Remote 로 변경
4. Build → New Build → 결과물을 CDN 에 업로드
5. 서버 `Game.bundle_url` 에 CDN 의 catalog.json URL 입력
6. 클라이언트는 `Addressables.LoadContentCatalogAsync(url)` 로 catalog 갱신

## 트러블슈팅

| 증상 | 해결 |
|---|---|
| `AddressableAssetSettings is null` | Window → Asset Management → Addressables → Groups → "Create Addressables Settings" |
| `InvalidKeyException: demo/hello` | 데모 자산을 Addressable 로 마크 안 함. 위 #2 단계 |
| Editor 에서만 동작, 빌드에서 실패 | Addressables Build 안 함. 위 #3 단계 |
| `Failed to download catalog from ...` | Remote profile 사용 중 + URL 잘못됨. profile 확인 |
