# 숏게타 (ShortGameTown) — CLAUDE.md

> Claude Code가 이 프로젝트에서 작업할 때 항상 이 문서를 참조한다.
> 모든 코드 생성·수정·리팩터링은 아래 규칙을 따른다.

---

## 프로젝트 개요

**숏게타**는 밈/병맛 기반 미니게임 플랫폼이다.
- WarioWare식 자동 전환 세션 (2~3분)
- 넷플릭스식 UGC 확장 전략 (오리지널 → 파트너 → UGC)
- 태그 기반 추천 + DDA(다이나믹 난이도 조절)
- 글로벌/게임별 리더보드

**슬로건:** "한판만 더"

---

## 기술 스택

| 레이어 | 기술 | 비고 |
|--------|------|------|
| 클라이언트 | Unity 6.4 LTS (6000.4.1f1) | Android 우선 → iOS → Steam (URP, IL2CPP, ARM64) |
| 게임 비율 | 9:16 세로형 | 최소 해상도 720×1280 |
| 백엔드 | Go | REST API |
| 인프라 | k3s + ArgoCD | GitHub Actions CI/CD |
| 번들 배포 | Unity Addressables + CDN | UGC 게임 런타임 로드 |
| AI 워크플로우 | Claude Code + MCP for Unity | CoplayDev unity-mcp |

---

## 폴더 구조 (Unity 프로젝트 — 모노레포 `client/`)

```
client/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/              # 플랫폼 코어 (변경 금지)
│   │   │   ├── IMinigame.cs
│   │   │   ├── InputEvent.cs
│   │   │   ├── MinigameLauncher.cs
│   │   │   ├── MinigameSession.cs
│   │   │   ├── ScoreVariable.cs   # XOR 난독화 SafeInt/SafeFloat
│   │   │   ├── TimeSync.cs
│   │   │   └── TagSystem.cs
│   │   ├── Minigames/         # 오리지널 미니게임 (Iter 2A: 6개 모두 구현 완료)
│   │   │   ├── FrogCatch/     # frog_catch_v1   30s  max 1000
│   │   │   ├── NoodleBoil/    # noodle_boil_v1  45s  max 500
│   │   │   ├── PokerFace/     # poker_face_v1   60s  max 800
│   │   │   ├── DarkSouls/     # dark_souls_v1   30s  max 300
│   │   │   ├── KakaoUnread/   # kakao_unread_v1 20s  max 600
│   │   │   └── MathGenius/    # math_genius_v1  30s  max 1500
│   │   ├── UI/Mobile/         # BootstrapController, Toast 등
│   │   ├── Network/           # ApiClient, HmacSigner, *Api, JwtStore, ServerConfig, Models
│   │   └── ShortGeta.Runtime.asmdef
│   ├── Settings/              # URP 프로필
│   ├── Data/                  # ScriptableObject (ServerConfig-Dev 등)
│   ├── Scenes/                # Bootstrap.unity (사용자가 Editor 에서 생성)
│   ├── Prefabs/
│   └── Tests/
│       ├── EditMode/          # Hmac, ScoreVariable, TimeSync
│       └── PlayMode/          # MinigameLauncher 등
├── Packages/manifest.json     # UniTask, URP 17, Newtonsoft.Json, TestFramework
├── ProjectSettings/
└── README.md
```

---

## 핵심 인터페이스 — IMinigame.cs

> 이 인터페이스는 **절대 변경하지 않는다.**
> 모든 미니게임(오리지널+UGC)이 이것을 구현한다.

```csharp
public interface IMinigame
{
    string   GameId     { get; }  // "frog_catch_v1" — 소문자_스네이크_버전
    string   Title      { get; }  // "개구리 잡아라"
    string   CreatorId  { get; }  // "shotgeta_official" or UGC 크리에이터 ID
    float    TimeLimit  { get; }  // 제한시간 (초)
    string[] Tags       { get; }  // ["반응속도", "동물", "병맛"]

    void OnGameStart();
    void OnGameEnd();
    int  GetScore();
    void OnInput(InputEvent e);   // 터치/클릭/키보드 추상화
}
```

---

## 코드 컨벤션

### 네이밍
```
클래스/인터페이스  : PascalCase      (FrogCatchGame, IMinigame)
메서드/프로퍼티    : PascalCase      (OnGameStart, GetScore)
private 필드       : _camelCase      (_currentScore, _isRunning)
로컬 변수          : camelCase       (elapsedTime, tagList)
상수               : UPPER_SNAKE     (MAX_PLAYERS, DEFAULT_TIME)
GameId 문자열      : lower_snake_v숫자  ("frog_catch_v1")
```

### 파일 규칙
- 파일 1개 = 클래스 1개
- 파일명 = 클래스명 (FrogCatchGame.cs)
- 미니게임 클래스는 반드시 `MonoBehaviour`와 `IMinigame` 동시 구현

### 주석
```csharp
// TODO: [담당자] 설명         — 미완성 작업
// FIXME: [담당자] 설명        — 버그/문제
// NOTE: 설명                  — 중요 맥락 설명
// PERF: 설명                  — 성능 관련 주의사항
```

### 금지사항
- `Find()`, `FindObjectOfType()` 런타임 호출 금지 → Inspector 주입 사용
- `string` 연결 루프 금지 → `StringBuilder` 사용
- `Update()` 안에 LINQ 금지
- `public` 필드 금지 → `[SerializeField] private` 사용

---

## 미니게임 구현 체크리스트

새 미니게임 만들 때 반드시 확인:

```
[ ] IMinigame 인터페이스 전체 구현
[ ] GameId: lower_snake_v숫자 형식
[ ] Tags: 장르/강도/소재/밈도 중 각 1개 이상
[ ] TimeLimit: 15~90초 범위
[ ] 9:16 세로 레이아웃 적용
[ ] 튜토리얼 없이 3초 내 규칙 이해 가능한지 확인
[ ] 성공/실패 연출 (과장된 사운드 + 이펙트)
[ ] OnInput() 터치와 키보드 양쪽 처리
[ ] PlayMode 테스트 작성 (최소 start/end/score)
[ ] Addressable 그룹에 등록
```

---

## DDA (다이나믹 난이도 조절)

```
Success Rate (SR) = 최근 10게임 클리어 수 / 10

SR > 0.8  → 다음 세션 강도 태그 가중치 +1단계
SR < 0.4  → 다음 세션 강도 태그 가중치 -1단계
0.4~0.8   → 현재 유지

조절폭: ±1단계 제한 (급격한 변화 방지)
DDA는 유저에게 노출하지 않는다
```

---

## API 엔드포인트 (Go 백엔드)

```
GET  /v1/games              게임 목록 (태그 필터 지원)
GET  /v1/games/:id          게임 상세
POST /v1/sessions           세션 시작 (추천 큐 반환)
POST /v1/sessions/:id/end   세션 종료 + 점수 저장
GET  /v1/rankings/global    글로벌 랭킹
GET  /v1/rankings/:gameId   게임별 랭킹
POST /v1/analytics/event    플레이 이벤트 수집
```

---

## 하이라이트 녹화 규칙

> **Iter 2B (MVP 완료)**: Editor / Standalone 에서 PNG 시퀀스 + circular buffer 동작.
> 모바일 native plugin (MediaProjection / ReplayKit) + MP4 인코딩 + 워터마크 +
> 원탭 공유 는 Iter 2B' 후속.

- 녹화 대상: 클리어/실패 직전 **3초**
- 포맷: 9:16 MP4, 최대 10MB (현재 PNG 시퀀스, MP4 는 후속)
- 워터마크: 숏게타 로고 우하단 고정 (후속)
- Android: MediaRecorder API
- iOS: ReplayKit
- 통합: RecordingService 추상 클래스로 플랫폼 분기

---

## 작업 지시 패턴

Claude Code에게 지시할 때 이 패턴을 사용한다:

```bash
# 새 미니게임 생성
"FrogCatch 미니게임 만들어줘.
 IMinigame 구현, Tags: ["반응속도","동물","병맛"],
 TimeLimit: 30f, 체크리스트 전부 통과해야 함"

# 기존 코드 수정
"@MinigameLauncher.cs 에 WarioWare식 2초 카운트다운 추가.
 기존 인터페이스 변경 없이, 테스트도 업데이트"

# 버그 픽스
"@FrogCatchGame.cs OnInput()이 터치를 못 잡음.
 문제 찾고 PlayMode 테스트로 재현 후 수정"
```

---

## 주의사항

1. `IMinigame` 인터페이스는 수정하지 않는다
2. `Core/` 폴더 파일은 수정 전 반드시 확인 요청
3. 새 패키지 추가 시 `packages.json` 변경사항 명시
4. Addressables 그룹 구조는 임의로 바꾸지 않는다
5. 테스트 없는 미니게임 코드는 완성된 것이 아니다
