# 숏게타(ShortGameTown) — 개발 지침서

> 이 문서는 개발자가 코드 작성에만 집중할 수 있도록
> 환경 세팅부터 배포까지 모든 개발 절차를 단일 문서로 정의한다.
> 마스터 계획서: `shortgeta-plan-v1.3.html` (v1.3 기준)
>
> ⚠️ 본 문서 일부 섹션은 **이전 v3 설계(아카이브)** 의 멀티레포·`develop` 브랜치 가정을 기반으로 작성되었다.
> **레포 구조와 브랜치 전략은 아래 §1, §3 을 따른다.** v3 가정과 충돌하는 다른 섹션(CI/CD, 인프라 운영 등)은
> 향후 모노레포 + `dev`/`qa`/`real` 브랜치 기반으로 갱신될 예정.

---

## 목차

1. [레포지토리 구조](#1-레포지토리-구조)
2. [로컬 개발 환경 세팅](#2-로컬-개발-환경-세팅)
3. [Git 브랜치 전략](#3-git-브랜치-전략)
4. [Go 서버 개발 규칙](#4-go-서버-개발-규칙)
5. [Unity 클라이언트 개발 규칙](#5-unity-클라이언트-개발-규칙)
6. [테스트 규칙](#6-테스트-규칙)
7. [CI/CD 파이프라인 사용법](#7-cicd-파이프라인-사용법)
8. [인프라 운영 규칙](#8-인프라-운영-규칙)
9. [시크릿 및 환경변수 관리](#9-시크릿-및-환경변수-관리)
10. [장애 대응 절차](#10-장애-대응-절차)

---

## 1. 레포지토리 구조

**모노레포 — `gensdeis/SGT` 단일 레포 안에 4개 프로젝트 디렉토리 구분.**
상세는 `GIT_STRATEGY.md` 참조.

| 디렉토리 | 역할 |
|---|---|
| `client/` | Unity 클라이언트 (Unity 2022.3 LTS, Addressables) |
| `server/` | Go 게임 서버 (Game Registry / Ranking / Analytics / Creator API) |
| `ops-tool/` | 게임 운영툴 (Phase 2~ UGC 심사 포함) |
| `infra/` | k8s 매니페스트 + ArgoCD App-of-Apps + GitHub Actions |
| `Docs/` | 계획·규칙·가이드 |

> CI/CD 는 path filter 로 변경된 디렉토리에 따라 분기한다.
> ArgoCD 는 `infra/clusters/<env>/` 를 추적한다.

---

## 2. 로컬 개발 환경 세팅

### 사전 설치 항목

```bash
# 필수
brew install go         # Go 1.21+
brew install docker     # Docker Desktop
brew install kubectl    # K8s CLI
brew install helm       # Helm
brew install terraform  # Terraform
brew install kubeseal   # Sealed Secrets CLI

# Go 도구
go install github.com/sqlc-dev/sqlc/cmd/sqlc@latest
go install github.com/pressly/goose/v3/cmd/goose@latest
```

### 로컬 의존성 실행 (docker-compose)

```bash
# minigame-server 레포 루트에서
docker-compose up -d

# 상태 확인
docker-compose ps

# 로그 확인
docker-compose logs -f postgres
```

**접속 정보**

| 서비스 | 주소 | 비고 |
|---|---|---|
| PostgreSQL | `localhost:5432` | 서버 코드에서 직접 연결 |
| Redis | `localhost:6379` | 서버 코드에서 직접 연결 |
| Adminer (DB GUI) | `http://localhost:8081` | 브라우저에서 데이터 확인 |

> Adminer 접속: 서버 `postgres`, 사용자 `minigame`, 비밀번호 `localpass`, DB `minigame_dev`

### 환경변수 설정

```bash
# .env.local 파일 생성 (git 에 커밋하지 않음)
cp .env.example .env.local

# .env.local 내용
DATABASE_URL=postgres://minigame:localpass@localhost:5432/minigame_dev
REDIS_URL=redis://localhost:6379
STEAM_MOCK=true
HMAC_BASE_KEY=local-dev-base-key
BUILD_GUID=local-dev-build-guid
```

### DB 마이그레이션

```bash
# 마이그레이션 적용
goose -dir db/migrations postgres $DATABASE_URL up

# 마이그레이션 상태 확인
goose -dir db/migrations postgres $DATABASE_URL status

# 롤백 (1단계)
goose -dir db/migrations postgres $DATABASE_URL down
```

### 서버 실행

```bash
# 환경변수 로드 후 실행
source .env.local && go run ./cmd/server

# 또는 Air (핫 리로드) 사용
go install github.com/cosmtrek/air@latest
source .env.local && air
```

---

## 3. Git 브랜치 전략

상세 정책: `GIT_STRATEGY.md`

### 브랜치 구조

| 브랜치 | 용도 | 푸시 정책 |
|--------|------|-----------|
| `main` | 백업 통합 | 권한 없이 직접 푸시 가능 |
| `dev` | 개발 (ArgoCD local 클러스터 추적 대상) | PR 필요 |
| `qa` | 라이브 서비스 전 검증 | PR 필요 |
| `real` | 현재 라이브(real) | PR 필요 |
| `hotfix` | 긴급 핫픽스용 | PR 필요 |
| `pre` | 이전 버전 롤백용 | PR 필요 |

### 기본 흐름

```
feature/기능명 → dev (PR) → qa (PR) → real (PR, 태그 생성) → 배포
                                  └─ hotfix → real + dev (PR)
```

### 브랜치 생성 규칙

```bash
# 기능 개발
git checkout dev
git pull
git checkout -b feature/score-validation

# 버그 수정
git checkout dev
git checkout -b fix/hmac-timestamp-drift

# 긴급 핫픽스 (real 기준)
git checkout real
git checkout -b hotfix/leaderboard-timeout
```

### 커밋 메시지 규칙

```
<타입>: <요약> (50자 이하)

<본문> (선택, 72자 줄바꿈)

<꼬리말> (선택)
```

**타입 목록**

| 타입 | 용도 |
|---|---|
| `feat` | 새 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 동작 변경 없는 코드 개선 |
| `test` | 테스트 추가/수정 |
| `docs` | 문서 수정 |
| `chore` | 빌드/설정/의존성 변경 |
| `perf` | 성능 개선 |

**예시**

```bash
git commit -m "feat: HMAC 서명 검증에 타임스탬프 허용 오차 60초 적용"
git commit -m "fix: c.Context() 를 c.UserContext() 로 교체 — pgx 타입 에러 수정"
git commit -m "chore: docker-compose 에 adminer 서비스 추가"
```

### PR 규칙

- `feature/*` → `develop`: 셀프 리뷰 후 머지 가능 (1인 개발 시)
- `develop` → `main`: 반드시 테스트 통과 확인 후 머지
- `hotfix/*` → `main` + `develop`: 양쪽에 동시 머지

### 태그 및 릴리스

```bash
# 릴리스 태그 생성 (prod 배포 트리거)
git tag -a v1.0.0 -m "Release v1.0.0 — 미니게임 25종 추가"
git push origin v1.0.0
```

---

## 4. Go 서버 개발 규칙

### 디렉터리 구조

```
go-minigame/
├── cmd/server/             # main.go — DI 조립 + Graceful Shutdown
├── internal/               # 외부에서 import 불가한 핵심 비즈니스 로직
│   ├── auth/               # Steam 인증, 세션 관리
│   ├── score/              # 점수 검증 (HMAC + yaml 규칙)
│   ├── leaderboard/        # Steam Leaderboard Worker Pool
│   ├── analytics/          # 지표 수집 Worker Pool
│   └── metrics/            # Prometheus 커스텀 메트릭
├── pkg/                    # 외부에서 import 가능한 유틸
│   ├── steamapi/
│   │   ├── interface.go    # SteamAPI 인터페이스
│   │   ├── real.go         # 실제 구현체
│   │   └── mock.go         # Mock 구현체
│   ├── hmac/               # Signature 유틸
│   └── db/                 # pgx Pool 초기화
├── db/
│   ├── queries/            # sqlc SQL 쿼리
│   └── migrations/         # goose 마이그레이션
├── config/
│   └── games.yaml          # 게임별 검증 설정
├── docker-compose.yml
└── .env.example
```

### 필수 코딩 규칙

**① Fiber Context 구분**

```go
// ❌ 금지 — fasthttp.RequestCtx 반환, pgx 에 전달 불가
h.repo.FindOrCreate(c.Context(), steamID)

// ✅ 필수 — 표준 context.Context 반환
h.repo.FindOrCreate(c.UserContext(), steamID)
```

> DB 쿼리, 외부 API 호출이 있는 모든 곳에서 `c.UserContext()` 를 사용한다.
> `c.Context()` 는 Fiber/fasthttp 내부 전용으로만 사용한다.

**② 인터페이스 기반 의존성 주입**

비즈니스 로직(Handler) 안에 환경변수 분기나 Mock 코드를 절대 넣지 않는다.
구현체 선택은 `main.go` 한 곳에서만 한다.

```go
// ❌ 금지 — 핸들러 안에 분기 코드
func (h *AuthHandler) Login(c *fiber.Ctx) error {
    if os.Getenv("STEAM_MOCK") == "true" { // 핸들러에 이런 코드 금지
        ...
    }
}

// ✅ 필수 — main.go 에서만 분기
var steamClient steamapi.SteamAPI
if os.Getenv("STEAM_MOCK") == "true" {
    steamClient = &steamapi.MockSteamClient{FixedSteamID: "76561198000000001"}
} else {
    steamClient = steamapi.NewRealSteamClient(os.Getenv("STEAM_API_KEY"))
}
authHandler := auth.NewHandler(steamClient, userRepo, sessionStore)
```

**③ 구조화 로깅**

```go
// ❌ 금지 — 평문 로그
log.Printf("score submit failed for user %s", steamID)
fmt.Println("error:", err)

// ✅ 필수 — slog 구조화 로그
slog.Error("score submit failed",
    "steam_id",  steamID,
    "game_id",   req.GameID,
    "score",     req.Score,
    "error",     err,
)
```

**④ 고루틴 직접 생성 금지**

```go
// ❌ 금지 — 고루틴 무한 생성
go h.analytics.Record(req)

// ✅ 필수 — Worker Pool 의 Enqueue 사용
h.analyticsWorker.Enqueue(AnalyticsEvent{...})
```

**⑤ DB 쿼리는 sqlc 로만**

직접 SQL 문자열을 코드에 작성하지 않는다.
`db/queries/*.sql` 에 쿼리를 작성하고 `sqlc generate` 로 코드를 생성해서 사용한다.

```bash
# 쿼리 수정 후 반드시 실행
sqlc generate
```

**⑥ 새 미니게임 추가 시 코드 수정 금지**

점수 검증 설정은 `config/games.yaml` 만 수정한다. Go 코드는 건드리지 않는다.

```yaml
# config/games.yaml 에만 추가
games:
  - id: "new_game_id"
    min_play_seconds: 10
    max_score: 2000
    rate_limit_per_min: 8
```

### 새 API 엔드포인트 추가 절차

```
1. db/queries/ 에 필요한 SQL 작성
2. sqlc generate 실행
3. internal/<도메인>/ 에 Handler, Repository, Service 작성
4. pkg/steamapi/interface.go 변경 필요 시 real.go + mock.go 동시 업데이트
5. cmd/server/main.go 에 라우터 등록 + DI 연결
6. 테스트 작성
7. config/games.yaml 변경 필요 시 추가
```

### DB 마이그레이션 작성 규칙

```bash
# 마이그레이션 파일 생성
goose -dir db/migrations create add_game_events_table sql
```

- 파일명: `YYYYMMDDHHMMSS_설명.sql`
- `-- +goose Up` / `-- +goose Down` 양방향 반드시 작성
- 롤백 불가능한 작업(컬럼 삭제 등)은 팀 내 합의 후 진행

---

## 5. Unity 클라이언트 개발 규칙

### 필수 패키지

| 패키지 | 용도 | 설치 방법 |
|---|---|---|
| UniTask (Cysharp/UniTask) | 비동기 처리 | Package Manager → Git URL |
| Steamworks.NET | Steam 연동 | Package Manager |

### 비동기 처리 규칙

```csharp
// ❌ 금지 — 예외가 호출자에게 전달되지 않음
async void SubmitScore(int score) { ... }

// ✅ 필수 — UniTask 사용
async UniTaskVoid SubmitScore(int score) {
    try {
        await scoreApi.SubmitAsync(score)
            .Timeout(TimeSpan.FromSeconds(5)); // 타임아웃 항상 명시
    } catch (TimeoutException) {
        ToastUI.Show("네트워크 응답이 없습니다.");
    } catch (Exception e) {
        Debug.LogError(e);
        ToastUI.Show("오류가 발생했습니다.");
    }
}

// fire-and-forget 호출 시
void OnGameClear(int score) {
    SubmitScore(score).Forget(); // .Forget() 으로 의도 명시
}
```

### 점수 제출 규칙

- 서버 통신은 항상 Non-blocking 으로 처리한다
- 점수 제출 실패가 게임 진행 자체를 막아서는 안 된다
- 실패 시 토스트 메시지로만 안내한다 (팝업 금지)

### TimeSync 사용 규칙

로그인 직후 반드시 서버 시간을 동기화한다.
점수 제출 타임스탬프는 항상 `TimeSync.GetSyncedTimestamp()` 를 사용한다.

```csharp
// 로그인 성공 직후
TimeSync.Calibrate(loginResponse.server_time);

// 점수 제출 시
long timestamp = TimeSync.GetSyncedTimestamp(); // 로컬 시계 직접 사용 금지
```

### 점수/시간 변수 보호 규칙

- 점수, 플레이 시간 변수는 평문 `int` / `float` 로 선언하지 않는다
- XOR 난독화 래퍼 클래스로 감싸서 사용한다
- IL2CPP 빌드를 기본으로 사용한다

---

## 6. 테스트 규칙

### Go 서버 테스트

**유닛 테스트 필수 작성 대상**

| 대상 | 이유 |
|---|---|
| HMAC Signature 검증 | 보안 핵심 로직 |
| 점수/시간 범위 검증 | 어뷰징 방어 핵심 |
| Time Drift 보정 로직 | 경계값 테스트 필요 |
| Worker Pool Enqueue/Dequeue | 동시성 버그 방지 |

```bash
# 전체 테스트 실행
go test ./...

# 커버리지 확인
go test ./... -cover

# 특정 패키지만
go test ./internal/score/...

# 레이스 컨디션 검사 (Worker Pool 등 동시성 코드)
go test ./... -race
```

**Mock 사용 규칙**

- DB 호출이 필요한 테스트는 `pgxmock` 라이브러리 사용
- Steam API 호출은 `MockSteamClient` 주입
- 외부 HTTP 호출은 `httptest.NewServer` 사용

```go
func TestLogin_Success(t *testing.T) {
    mockSteam := &steamapi.MockSteamClient{FixedSteamID: "76561198000000001"}
    handler := auth.NewHandler(mockSteam, fakeUserRepo, fakeSession)

    resp := callLogin(handler, "any-ticket")
    assert.Equal(t, 200, resp.StatusCode)
}
```

### CI 테스트 통과 규칙

- `develop` 브랜치 push 시 GitHub Actions 테스트가 반드시 통과해야 한다
- 테스트 실패 시 이미지 빌드 및 배포가 진행되지 않는다
- 로컬에서 `go test ./... -race` 통과 확인 후 push 한다

---

## 7. CI/CD 파이프라인 사용법

### 자동 배포 조건

| 브랜치/태그 | 트리거 | 배포 대상 |
|---|---|---|
| `develop` push | server-ci.yml | dev 네임스페이스 |
| `main` push | server-ci.yml | prod 네임스페이스 |
| `main` push (client) | client-ci.yml | Steam (베타 브랜치) |
| `v*` 태그 생성 | client-ci.yml | Steam (기본 브랜치) |

### 배포 흐름 확인

```bash
# 1. GitHub Actions 실행 상태 확인
# https://github.com/myorg/minigame-server/actions

# 2. ArgoCD 동기화 상태 확인
kubectl get applications -n argocd

# 3. Pod 상태 확인
kubectl get pods -n dev    # dev 환경
kubectl get pods -n prod   # prod 환경

# 4. 배포 로그 확인
kubectl logs -n prod -l app=minigame-server --tail=100
```

### 배포 실패 시 롤백

```bash
# ArgoCD 로 이전 버전으로 롤백
argocd app rollback minigame-server-prod

# 또는 kubectl 로 직접 롤백
kubectl rollout undo deployment/minigame-server -n prod

# 롤백 상태 확인
kubectl rollout status deployment/minigame-server -n prod
```

### 수동 배포가 필요한 경우

원칙적으로 수동 배포는 하지 않는다.
긴급 상황에서만 아래 절차를 따른다.

```bash
# k8s-manifests 레포에서 이미지 태그 직접 수정 후 push
# → ArgoCD 가 자동으로 감지해 배포
git clone https://github.com/myorg/k8s-manifests
cd k8s-manifests
# overlays/prod/deployment-patch.yaml 의 image 태그 수정
git commit -am "hotfix: rollback to previous stable image"
git push
```

---

## 8. 인프라 운영 규칙

### k3s 클러스터 접근

```bash
# 클러스터 상태 확인
kubectl get nodes
kubectl get pods -A

# 네임스페이스별 리소스 확인
kubectl get all -n dev
kubectl get all -n prod
kubectl get all -n monitoring
kubectl get all -n argocd
```

### Terraform 운영 규칙

- 인프라 변경은 반드시 코드로 한다. AWS 콘솔 직접 수정 금지
- `terraform plan` 결과를 반드시 확인한 후 `terraform apply` 실행
- `terraform destroy` 는 dev 환경에서만 허용

```bash
cd terraform/environments/dev

# 변경 사항 미리 확인
terraform plan

# 적용
terraform apply

# 상태 확인
terraform show
```

### Sealed Secrets 운영 규칙

새 시크릿을 추가하거나 기존 값을 변경할 때의 절차:

```bash
# 1. 평문 Secret 작성
cat > secret.yaml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: minigame-secrets
  namespace: prod
type: Opaque
stringData:
  DATABASE_URL: "postgres://..."
  HMAC_BASE_KEY: "..."
EOF

# 2. Sealed Secret 으로 암호화
kubeseal --format yaml < secret.yaml > sealed-secret.yaml

# 3. 평문 파일 즉시 삭제
rm secret.yaml

# 4. 암호화된 파일만 git 에 커밋
git add sealed-secret.yaml
git commit -m "chore: update prod secrets"

# 5. 클러스터에 적용
kubectl apply -f sealed-secret.yaml
```

> ⚠️ `secret.yaml` (평문) 은 절대 git 에 커밋하지 않는다. `.gitignore` 에 `**/secret.yaml` 추가 필수

### Grafana 모니터링 확인

```bash
# Grafana 포트포워딩 (로컬에서 접근)
kubectl port-forward -n monitoring svc/monitoring-grafana 3000:80

# 브라우저에서 접속
open http://localhost:3000
# 기본 계정: admin / (Helm 설치 시 지정한 비밀번호)
```

**일상 확인 항목**

| 빈도 | 확인 내용 |
|---|---|
| 매일 | p99 응답시간, 5xx 에러율, 고루틴 수 |
| 배포 후 | Pod 재시작 여부, 에러 로그 스파이크 |
| 주 1회 | DB 연결 사용률, Worker 큐 깊이 추이 |

---

## 9. 시크릿 및 환경변수 관리

### 환경변수 목록

| 변수 | 설명 | 저장 위치 |
|---|---|---|
| `DATABASE_URL` | PostgreSQL 연결 문자열 | Sealed Secret |
| `REDIS_URL` | Redis 연결 문자열 | Sealed Secret |
| `STEAM_API_KEY` | Steam Publisher API Key | Sealed Secret |
| `HMAC_BASE_KEY` | HMAC 서명 베이스 키 | Sealed Secret |
| `BUILD_GUID` | 빌드별 고유 Salt | Sealed Secret |
| `STEAM_MOCK` | Steam Mock 모드 활성화 | ConfigMap (dev 만) |

### 관리 원칙

- 시크릿은 코드에 절대 하드코딩하지 않는다
- `.env.local` 은 로컬 전용이며 git 에 커밋하지 않는다 (`.gitignore` 에 포함)
- GitHub Actions 시크릿은 레포 Settings → Secrets 에서 관리한다
- Sealed Secrets 마스터 키는 AWS Secrets Manager 또는 1Password 에 백업한다

### GitHub Actions 시크릿 목록

| 시크릿 이름 | 용도 |
|---|---|
| `MANIFEST_REPO_TOKEN` | k8s-manifests 레포 쓰기 권한 PAT |
| `UNITY_LICENSE` | Unity 빌드 라이선스 |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |
| `STEAM_USERNAME` | Steam 배포 계정 |
| `STEAM_CONFIG_VDF` | Steam 배포 설정 |
| `STEAM_APP_ID` | Steam 앱 ID |

---

## 10. 장애 대응 절차

### 장애 등급

| 등급 | 기준 | 목표 복구 시간 |
|---|---|---|
| P1 | prod 서버 다운 / 점수 제출 전체 불가 | 30분 이내 |
| P2 | 리더보드 갱신 지연 / 일부 API 오류 | 2시간 이내 |
| P3 | 로그 수집 중단 / 모니터링 이상 | 다음 근무일 |

### P1 장애 대응 순서

```bash
# 1. 현재 상태 파악
kubectl get pods -n prod
kubectl describe pod <문제 파드> -n prod
kubectl logs <문제 파드> -n prod --previous

# 2. Grafana 에서 에러 발생 시점 특정
# Loki 쿼리: {namespace="prod"} |= "error" | json

# 3a. 코드 버그인 경우 → 이전 버전으로 즉시 롤백
argocd app rollback minigame-server-prod

# 3b. 인프라 문제인 경우 → Pod 재시작
kubectl rollout restart deployment/minigame-server -n prod

# 3c. DB 연결 문제인 경우 → Pool 상태 확인
# Grafana: db_pool_acquired_conns 메트릭 확인

# 4. 복구 확인
kubectl rollout status deployment/minigame-server -n prod
curl https://api.minigame.com/health
```

### 자주 발생하는 문제

**ImagePullBackOff**

```bash
# GHCR PAT 만료 여부 확인
kubectl get secret ghcr-secret -n prod -o yaml

# PAT 재발급 후 Secret 재생성
kubectl delete secret ghcr-secret -n prod
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=USERNAME \
  --docker-password=NEW_PAT \
  --namespace prod
```

**DB 연결 고갈**

```bash
# 현재 연결 수 확인 (Supabase 대시보드 또는)
kubectl exec -n prod <pod> -- env | grep DATABASE_URL
# Grafana: db_pool_acquired_conns / MaxConns 비율 확인

# 임시 조치: Pod 재시작으로 연결 반환
kubectl rollout restart deployment/minigame-server -n prod
```

**ArgoCD 동기화 실패**

```bash
# 상태 확인
argocd app get minigame-server-prod

# 강제 동기화
argocd app sync minigame-server-prod --force

# 매니페스트 문법 오류 확인
kubectl apply --dry-run=client -f overlays/prod/
```

---

## 부록 — 자주 쓰는 명령어 모음

```bash
# 로컬 개발
docker-compose up -d                          # 의존성 실행
source .env.local && go run ./cmd/server      # 서버 실행
go test ./... -race                           # 전체 테스트 + 레이스 컨디션 검사
sqlc generate                                 # DB 쿼리 코드 재생성
goose -dir db/migrations postgres $DATABASE_URL up  # 마이그레이션 적용

# 클러스터 운영
kubectl get pods -A                           # 전체 Pod 상태
kubectl logs -n prod -l app=minigame-server --tail=100  # prod 로그
argocd app list                               # ArgoCD 앱 목록
kubectl port-forward -n monitoring svc/monitoring-grafana 3000:80  # Grafana 접속

# 시크릿 관리
kubeseal --format yaml < secret.yaml > sealed-secret.yaml  # 암호화
kubectl get secret -n kube-system -l sealedsecrets.bitnami.com/sealed-secrets-key -o yaml > master-key-backup.yaml  # 마스터 키 백업

# 배포 관련
git tag -a v1.0.0 -m "Release v1.0.0"        # 릴리스 태그
git push origin v1.0.0                        # prod 배포 트리거
argocd app rollback minigame-server-prod      # 롤백
kubectl rollout status deployment/minigame-server -n prod  # 배포 상태 확인
```
