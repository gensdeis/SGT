# 숏게타 — Prompt Engineering 가이드

> Claude Code / Claude Desktop + MCP for Unity 작업 시
> 일관된 결과물을 위한 프롬프트 패턴 모음.
> 작업 전 이 파일을 확인한다.

---

## 시스템 프롬프트 (매 세션 시작 시 붙여넣기)

```
이 프로젝트는 Unity 2022.3 LTS + Go 백엔드로 구성된
"숏게타(ShortGameTown)" 밈 기반 미니게임 플랫폼이다.

규칙:
1. 코드는 CLAUDE.md의 컨벤션을 따른다
2. IMinigame 인터페이스는 절대 변경하지 않는다
3. 새 미니게임은 체크리스트를 통과해야 한다
4. 모든 코드에 PlayMode 또는 EditMode 테스트를 작성한다
5. Core/ 폴더 수정은 먼저 나에게 확인한다
```

---

## 작업 유형별 프롬프트 템플릿

### 1. 새 미니게임 생성

```
[게임명] 미니게임 스크립트를 만들어줘.

- GameId: "[game_id_v1]"
- Title: "[한국어 제목]"
- Tags: ["[장르]", "[소재]", "[밈도]"]
- TimeLimit: [초]f
- 게임 설명: [한 줄 설명]
- 핵심 인터랙션: [탭/드래그/키보드 등]

Assets/Scripts/Minigames/[GameName]/ 폴더에 생성.
IMinigame 체크리스트 전부 통과.
PlayMode 테스트 포함.
```

**예시:**
```
FrogCatch 미니게임 스크립트를 만들어줘.

- GameId: "frog_catch_v1"
- Title: "개구리 잡아라"
- Tags: ["반응속도", "동물", "병맛"]
- TimeLimit: 30f
- 게임 설명: 화면에 랜덤 등장하는 개구리를 탭하면 점수
- 핵심 인터랙션: 터치(모바일) / 마우스 클릭(PC)

Assets/Scripts/Minigames/FrogCatch/ 폴더에 생성.
IMinigame 체크리스트 전부 통과.
PlayMode 테스트 포함.
```

---

### 2. 기존 파일 수정

```
@[파일명.cs] 에서 [기능]을 [변경 내용]으로 수정해줘.
- 기존 인터페이스/시그니처 변경 없이
- 영향받는 테스트도 함께 업데이트
- 변경 이유를 주석으로 남겨줘
```

**예시:**
```
@MinigameLauncher.cs 에서 게임 전환 시
WarioWare식 2초 카운트다운 연출을 추가해줘.
- 기존 Launch(IMinigame) 시그니처 변경 없이
- 카운트다운 중 입력 차단
- 관련 테스트 업데이트
```

---

### 3. 버그 수정

```
@[파일명.cs] 에서 [증상]이 발생함.
1. 원인 분석해줘
2. PlayMode 테스트로 버그 재현 코드 먼저 작성
3. 수정 후 테스트 통과 확인
```

---

### 4. 씬/프리팹 생성 (MCP Unity 직접 조작)

```
[씬/오브젝트명] 씬을 생성해줘.
- 위치: Assets/Scenes/Minigames/
- 포함할 GameObject: [목록]
- Canvas 설정: ScreenSpace-Camera, 9:16 레퍼런스 해상도 720x1280
- [MinigameName]Game 스크립트 attach
```

---

### 5. API 클라이언트 코드

```
Go 백엔드의 [엔드포인트] API를 호출하는
Unity C# 클라이언트 코드를 만들어줘.
- Assets/Scripts/Network/ 에 위치
- UniTask 또는 Coroutine 비동기 처리
- 에러 핸들링 포함 (NetworkError, TimeoutError)
- 응답 모델 클래스도 함께 생성
```

**예시:**
```
Go 백엔드의 POST /v1/sessions API를 호출하는
Unity C# 클라이언트 코드를 만들어줘.
- Assets/Scripts/Network/SessionApi.cs
- UniTask 비동기 처리
- 에러 핸들링 포함
- SessionStartRequest, SessionStartResponse 모델 클래스 포함
```

---

### 6. 테스트 작성

```
@[파일명.cs] 의 [클래스명]에 대한
[EditMode/PlayMode] 테스트를 작성해줘.
- Assets/Tests/[EditMode|PlayMode]/ 에 위치
- 테스트 케이스: [목록]
```

---

### 7. 리팩터링

```
@[파일명.cs] 를 아래 기준으로 리팩터링해줘.
- 기존 public API 변경 없이
- [구체적인 개선 목표]
- 리팩터링 전후 동작 보장하는 테스트 포함
```

---

## 하지 말아야 할 프롬프트 패턴

```
❌ "IMinigame 인터페이스에 Difficulty 프로퍼티 추가해줘"
   → 인터페이스 변경은 금지. DDA는 외부에서 태그로 제어.

❌ "Core 폴더에 있는 MinigameLauncher 바꿔줘"
   → 먼저 "바꾸려는 이유와 방법을 알려줘" 로 시작.

❌ "게임 빠르게 만들어줘 테스트는 나중에"
   → 테스트 없는 미니게임은 완성된 것이 아님.

❌ "Update()에서 FindObjectOfType 써줘"
   → 금지 패턴. Inspector 주입으로 대체 요청.
```

---

## 세션 시작 체크리스트

Claude Code 세션 시작 전:

```
[ ] CLAUDE.md 컨텍스트 로드됐는지 확인
    (claude --context CLAUDE.md 또는 프로젝트 루트에 위치)
[ ] Unity Editor 실행 중인지 확인 (MCP 연결용)
[ ] 작업할 파일의 현재 상태 파악
    ("@파일명 의 현재 구조 요약해줘"로 시작)
[ ] 작업 범위 명시 (어떤 파일을 건드리는지)
```

---

## 자주 쓰는 단축 지시어

```
"체크리스트 돌려줘"        → IMinigame 구현 체크리스트 검사
"테스트 돌려줘"            → 관련 테스트 실행 결과 확인
"현재 구조 요약해줘"       → 파일/폴더 구조 파악
"인터페이스 확인해줘"      → IMinigame 구현 여부 검사
"번들 등록해줘"            → Addressables 그룹에 추가
```

---

## 버전 히스토리

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| v1.0 | 2026.04 | 초안 작성 |
