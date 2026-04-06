# 숏게타 — Unity 클라이언트 (`client/`)

`Docs/PROJECT_PLAN.md` Phase 1, Iteration 1 구현체.

| 항목 | 값 |
|---|---|
| Unity | 6000.4.1f1 (Unity 6 LTS) |
| Render Pipeline | URP (Universal Render Pipeline) |
| Target Platform | Android (Min API 24, Target 34, IL2CPP, ARM64) |
| 비율 | 9:16 세로 고정 |
| 비동기 | UniTask |
| JSON | Newtonsoft.Json |
| 테스트 | Unity Test Framework (EditMode + PlayMode) |

## 빠른 시작

### 1. Pre-flight (1회)

**Unity Hub** → Installs → `6000.4.1f1` → ⚙️ → Add modules:
- ✅ Android Build Support
  - ✅ Android SDK & NDK Tools
  - ✅ OpenJDK

### 2. 프로젝트 열기

Unity Hub → **Open** → `F:\SGT\client` 폴더 선택.
첫 import 는 5~10분 (Library 생성).

### 3. 서버 연결

다른 터미널에서 kind 클러스터의 server 를 port-forward:

```bash
kubectl -n shortgeta-dev port-forward svc/shortgeta-server 18081:80
```

`Assets/Data/ServerConfig-Dev.asset` 의 base URL 이 `http://localhost:18081`
이므로 별도 설정 변경 불필요.

### 4. 실행

- Unity Editor 가 import 를 마치면, **첫 실행 시 Editor 가 자동으로 Bootstrap 씬을 만들지 않으므로** 사용자가 한 번만 셋업:
  1. **File → New Scene** → Empty 선택 → Save as `Assets/Scenes/Bootstrap.unity`
  2. Hierarchy 에 빈 GameObject 추가 (이름 `Bootstrap`)
  3. `Bootstrap` GameObject 에 `BootstrapController` 컴포넌트 add
  4. Inspector 에서 `Server Config` 슬롯에 `Assets/Data/ServerConfig-Dev` 드래그
- Scene 열고 ▶ Play

### 5. 기대 흐름 (Iter 2A: 풀 세션)

```
[Bootstrap] 시작
  └─ device id (PlayerPrefs 에 저장된 GUID 또는 신규 생성)
  └─ POST /v1/auth/device → JWT 저장
  └─ TimeSync 캘리브레이션
  └─ MinigameRegistry 에 6개 게임 등록
  └─ Home UI 활성화 (programmatic 생성)
        └─ "▶ 한판 더" 클릭
        └─ POST /v1/sessions → 추천 큐 수신 (5개 game_id)
        └─ MinigameSession 으로 5게임 순차 실행 (각각 2초 카운트다운)
        └─ 5개 점수 각각 HMAC 서명 → 단일 POST /v1/sessions/:id/end 일괄 제출
        └─ Result UI 에 게임별 점수 + 합계
```

> **빠른 디버그 모드**: BootstrapController Inspector 의 `Run Frog Catch Only`
> 토글을 켜면 frog_catch 1판만 실행. 풀 세션은 약 3분 걸려서 매 빌드마다
> 돌리기 비현실적이라 토글 제공.

## 구현된 미니게임 (6개)

| GameId | 한국어 | 시간 | 최대 점수 | 한 줄 설명 |
|---|---|---|---|---|
| frog_catch_v1 | 개구리 잡아라 | 30s | 1000 | 랜덤 등장 개구리 탭 |
| noodle_boil_v1 | 라면 끓이지 마라 | 45s | 500 | 5라운드 progress bar 60~80% 정확도 |
| poker_face_v1 | 포커페이스 유지 | 60s | 800 | 매초 +14, 가짜 보상 버튼 -100 |
| dark_souls_v1 | Dark Souls 도전 | 30s | 300 | 1/10 확률 +30, 실패 시 YOU DIED |
| kakao_unread_v1 | 카톡 읽씹하기 | 20s | 600 | 매초 +30, 가짜 알림 -50 |
| math_genius_v1 | 수학 천재 도전 | 30s | 1500 | 1자리수 +/- 4지선다 |

## Addressables (Iter 2C MVP)

`com.unity.addressables 2.10.0` 패키지가 설치되어 있고, `IBundleLoader` 추상화를
통해 사용 가능합니다. 현재는 인프라 세팅 + 데모 자산 1개 동작 검증 단계입니다.
미니게임 prefab 화는 Iter 2C' 에서 진행 예정.

### 사용자 1회 액션 — 데모 자산 마크

`Assets/Demo/HelloAddressable.txt` 파일이 commit 되어 있습니다. 이 파일을 한 번
Addressable 로 마크해야 데모 동작:

1. Project 패널 → `Assets/Demo/HelloAddressable.txt` 클릭
2. Inspector 상단 **Addressable** 체크박스 ✅
3. 우측 입력란에 **`demo/hello`** 입력 → 다른 곳 클릭

상세 절차: `client/docs/addressables-build.md`

### Editor Play 시 기대 로그

```
[Bundles] AddressableBundleLoader.InitializeAsync OK
[Bundles] Hello from Addressable bundle!
[Bundles] manifest registered 6 entries (non-empty: 0)
```

> "non-empty: 0" 은 정상 — 서버 `bundle_url` 이 모두 빈 문자열. Iter 2C' 에서
> 미니게임 prefab 을 실제 bundle 로 만들고 서버에 URL 등록 시 0 → 6 으로 변함.

## 하이라이트 녹화 (Iter 2B MVP)

각 미니게임 종료 시 직전 ~3초 (10fps × 30 프레임) 가 자동 캡처되어 PNG 시퀀스로
저장됩니다. Result UI 의 **📁 하이라이트 보기** 버튼으로 폴더를 열 수 있습니다.

| 항목 | 값 |
|---|---|
| 캡처 fps | 10 |
| 버퍼 크기 | 30 프레임 (3초) |
| 저장 위치 | `Application.persistentDataPath/highlights/{timestamp}_{gameId}/` |
| 형식 | PNG 시퀀스 (`frame_000.png ~ frame_029.png`) |
| 동작 환경 | Unity Editor / Windows / Mac / Linux Standalone |
| 모바일 | NativeStub (Iter 2B' 에서 MediaProjection/ReplayKit native plugin 으로 교체) |

> Editor 에서 "하이라이트 보기" 클릭 시 OS 파일 탐색기로 폴더가 열림.
> 모바일 빌드에서는 stub 동작 — UI 는 Toast 안내 표시.

콘솔에 단계별 로그 출력. PlayerPrefs 클리어:
`Edit → Clear All PlayerPrefs`

## 디렉토리 구조

```
client/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # IMinigame, MinigameLauncher, Session, ScoreVariable, TimeSync (변경 금지)
│   │   ├── Network/           # ApiClient, HmacSigner, *Api (서버 연동)
│   │   ├── UI/Mobile/         # BootstrapController, Home, SessionRunner, Result, Toast
│   │   └── Minigames/
│   │       └── FrogCatch/     # 오리지널 미니게임 1번
│   ├── Settings/              # URP 프로필
│   ├── Data/                  # ScriptableObject 데이터 (ServerConfig 등)
│   ├── Scenes/                # 씬 파일 (사용자가 Editor 에서 생성)
│   ├── Prefabs/
│   └── Tests/
│       ├── EditMode/          # HmacSigner, ScoreVariable, TimeSync 등
│       └── PlayMode/          # MinigameLauncher, FrogCatch smoke
├── Packages/manifest.json     # UniTask, URP, TestFramework, Newtonsoft.Json
├── ProjectSettings/
└── README.md
```

## 코딩 규칙

`Docs/CLAUDE.md` §"코드 컨벤션" 준수. 핵심:
- `IMinigame` 인터페이스 **수정 금지**
- `Find()`/`FindObjectOfType()` 런타임 호출 금지 → Inspector 주입
- `Update()` 안 LINQ 금지
- `public` 필드 금지 → `[SerializeField] private`
- 비동기는 `UniTask` + `.Forget()` + `.Timeout(5s)` 패턴
- 점수 변수는 `ScoreVariable` (XOR 난독화) 사용

## 테스트

```
Window → General → Test Runner
  EditMode → Run All
  PlayMode → Run All
```

CI 빌드는 Iteration 2 이후 GameCI 도입 시 추가. 현재는 로컬만.

## 빌드

```
File → Build Settings → Android → Switch Platform
File → Build Settings → Build → client/Build/shortgeta.apk
```

## HMAC ↔ 서버 일치

`HmacSigner.cs` 의 `BuildSecretKey` 는 `BUILD_GUID` 환경값을 사용.
서버 측 (`server/deploy/base/secret.yaml`) 의 `BUILD_GUID=local-dev-build-guid`
와 동일해야 점수 검증 통과. 불일치 시 401 또는 검증 거부 (`anticheat: invalid signature`).

dev 단계에서는 `ServerConfig-Dev.asset` 의 `BuildGuid` 필드에 같은 값을 박아둔다.
프로덕션은 `BuildSettings → Player → Other → Scripting Define Symbols` 등으로 분리.

## 후속 (Iteration 2~)

- 나머지 5개 미니게임 (`noodle_boil`, `poker_face`, `dark_souls`, `kakao_unread`, `math_genius`)
- 9:16 하이라이트 녹화 (MediaRecorder + ReplayKit)
- DDA 클라이언트 통합 + 추천 큐 활용
- Steam 빌드 + Steam Ticket 인증
- GameCI 워크플로우
- AdMob/Unity Ads SDK + 광고제거 IAP
