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

### 5. 기대 흐름

```
[Bootstrap] 시작
  └─ device id (PlayerPrefs 에 저장된 GUID 또는 신규 생성)
  └─ POST /v1/auth/device → JWT 저장
  └─ TimeSync 캘리브레이션
  └─ Home UI 활성화 (programmatic 생성)
        └─ "▶ 한판 더" 클릭
        └─ POST /v1/sessions → 추천 큐 수신
        └─ FrogCatch 30초 플레이
        └─ 점수 HMAC 서명 → POST /v1/sessions/:id/end
        └─ Result UI → 다시
```

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
