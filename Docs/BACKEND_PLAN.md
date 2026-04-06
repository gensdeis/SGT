# 숏게타 — Go 백엔드 구현 계획

> 계획서 v1.3 (`shortgeta-plan-v1.3.html`) 기준.
> Phase별 로드맵과 일치하도록 백엔드 범위를 정의한다.

---

## 기술 스택

| 항목 | 선택 |
|------|------|
| 언어 | Go 1.21+ |
| 웹 프레임워크 | Fiber |
| DB | PostgreSQL (Phase 1: Supabase 무료 → Phase 2 유료 → Phase 3 RDS) |
| DB 드라이버 | pgx v5 (pgxpool) |
| ORM/쿼리 | sqlc (SQL → Go 자동 생성) |
| 마이그레이션 | goose |
| 캐시 / Rate Limit | Redis (Upstash 무료 → 사용량 기반) |
| 인증 | 디바이스 토큰 + JWT (Phase 2 Steam Ticket 추가, Phase 2~ SSO 검토) |
| Anti-cheat | HMAC 동적 키 파생 + 서버 측 검증 (계획서 v1.3 §11) |
| 로깅 | `log/slog` (JSON 구조화) |
| 메트릭 | Prometheus (Fiber 미들웨어 + 커스텀 게이지) |
| 인프라 | k3s + ArgoCD, GitHub Actions CI/CD, Traefik, Sealed Secrets |
| 저장소 위치 | 모노레포 `server/` (GIT_STRATEGY.md 참조) |

---

## API 도메인 (v1.3 §11)

계획서는 백엔드를 **Game Registry / Ranking / Analytics / Creator** 4개 도메인으로 정의한다.

### 1. Game Registry API
```
GET  /v1/games              게임 목록 (태그 필터)
GET  /v1/games/:id          게임 상세 (Addressable 번들 메타 포함)
```
- 오리지널 + (Phase 3) UGC 게임 메타데이터
- 태그/강도/소재/밈도 필터링
- Addressable 번들 URL · 버전 · 해시 제공

### 2. Session / Ranking API
```
POST /v1/sessions           세션 시작 (추천 큐 반환)
POST /v1/sessions/:id/end   세션 종료 + 점수 저장 (HMAC 서명)
GET  /v1/rankings/global    글로벌 랭킹
GET  /v1/rankings/:gameId   게임별 랭킹
```
- DDA 강도 태그 포함한 추천 큐 응답
- 점수 제출은 HMAC 서명 검증 필수
- 주간/월간/명예의전당/수치의전당 리셋 잡

### 3. Analytics API
```
POST /v1/analytics/event    플레이 이벤트 수집
```
- 완료/이탈/재도전/SR 이벤트
- 태그 가중치 계산 + DDA 입력으로 사용
- 배치 적재 (세션 종료 후)

### 4. Creator API (Phase 3)
```
POST /v1/creator/games      UGC 게임 등록
POST /v1/creator/bundles    번들 업로드 (CDN 연동)
GET  /v1/creator/reviews    심사 상태 조회
```
- Creator SDK + 자동 필터(저작권/혐오/성인) 연동
- 운영툴(`ops-tool/`)과 2단계 심사 워크플로우 공유

---

## Phase별 백엔드 범위

### Phase 1 — Android 출시 (지금 시작 · 6~9개월)
계획서 v1.3 §12 Phase 1에서 백엔드 항목:

- [x] Game Registry API (오리지널 게임 등록) — `internal/game`, `config/games.yaml` 6개 게임
- [x] Ranking API — 게임별 + 글로벌 (핵심) — `internal/ranking` + Worker Pool
- [x] Analytics API — 이벤트 수집 + 태그 가중치 입력 — `internal/analytics` + Worker Pool (1000 buf, 5 workers, 100/5s flush)
- [x] DDA 기초 로직 (서버 측 강도 태그 산출) — `internal/dda` + 추천기 통합
- [x] HMAC 점수 서명 검증 (Anti-cheat 기반) — `pkg/hmac` + `internal/anticheat` (HMAC + games.yaml + Redis rate limit 3단계)
- [x] k3s + ArgoCD 배포 파이프라인 — `server/deploy/{base,overlays/dev}` + `infra/apps/shortgeta-server/application.yaml`
- [x] 디바이스 인증 + 광고제거 영수증 검증($2.99) — `internal/auth` (device→JWT) + `internal/purchase` (Mock 동작, Real skeleton)

> Iteration 1 결과: `go run ./cmd/api` 로 동작, kind 클러스터에 ArgoCD 배포 검증 완료.
> 후속 (Iteration 2): GHCR push 자동화, SealedSecret 적용, Google Play Real verifier 구현.

> UGC 저작권·심사 정책은 **문서화만** (구현은 Phase 3)

### Phase 2 — Steam + iOS (+6개월)
- [ ] DDA SR 기반 강도 자동 조정 고도화
- [ ] 시즌제 글로벌 랭킹 + 엠블럼 부여
- [ ] 친구 리더보드 (소셜 그래프)
- [ ] 커뮤니티 신고 시스템 (v1.3 신규)
- [ ] Steam 영수증 검증 + 독점 콘텐츠 게이팅
- [ ] 파트너 크리에이터 베타용 내부 등록 API

### Phase 3 — UGC 오픈 (+12개월)
- [ ] Creator API 전면 공개
- [ ] 자동 필터링 파이프라인 (저작권/혐오/성인)
- [ ] 2단계 심사 워크플로우 (자동 → 운영툴 수동)
- [ ] 크리에이터 수익 분배 (YouTube식)
- [ ] Steam 구매자 UGC 조기 접근 게이팅
- [ ] 추천 알고리즘 A/B 테스트 인프라

---

## 디렉토리 구조 (예정)

```
server/
├── cmd/
│   ├── api/              # main.go — DI 조립 + Graceful Shutdown
│   └── worker/           # 배치/리셋 잡
├── internal/             # 외부 import 불가
│   ├── game/             # Game Registry 도메인
│   ├── session/          # Session / Ranking
│   ├── analytics/        # Analytics 이벤트 (Worker Pool)
│   ├── creator/          # Creator (Phase 3)
│   ├── dda/              # 강도 조절 로직
│   ├── anticheat/        # HMAC 검증
│   └── metrics/          # Prometheus 커스텀 메트릭
├── pkg/                  # 외부 import 가능 유틸
│   ├── hmac/             # Signature 생성/검증
│   └── db/               # pgxpool 초기화 + 설정
├── db/
│   ├── queries/          # sqlc SQL
│   └── migrations/       # goose
├── config/
│   └── games.yaml        # 게임별 검증 설정값
└── deploy/               # k8s overlays (dev/qa/real) — ArgoCD가 추적
```

---

## Anti-cheat — HMAC 동적 키 파생

(v3 설계 흡수. 클라이언트 메모리 변조 + Replay Attack 동시 방어)

### 검증 레이어 구조 (3단계)

```
레이어 1 (클라이언트)  — 점수/시간 변수 XOR 난독화 + IL2CPP
레이어 2 (전송 구간)   — HMAC Signature 무결성 검증
레이어 3 (서버)        — 시간/점수 범위 + Rate Limit (games.yaml)
```

세 레이어 모두 통과해야 점수가 랭킹에 반영된다.

### Secret Key 동적 생성

Secret 을 코드에 하드코딩하면 IL2CPP 를 써도 결국 추출된다.
**런타임에 여러 조각을 조합해 동적으로 파생** + **게임별로 다른 키** 를 만든다.

```csharp
// Unity (클라이언트) — BuildSecretKey
private static string BuildSecretKey(string gameId) {
    string[] parts = { "m1n1", "G4me", "S3cr" };          // 정적 분석 방해
    string baseKey = string.Join("-", parts);
    string salt = Application.buildGUID;                  // 빌드별 고유값
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(baseKey));
    byte[] derived = hmac.ComputeHash(Encoding.UTF8.GetBytes(gameId + salt));
    return Convert.ToHexString(derived).ToLower();
}
```

```go
// Go (서버) — 동일 파생 로직
func (v *Validator) deriveSecretKey(gameID string) string {
    base := os.Getenv("HMAC_BASE_KEY")    // Sealed Secret 으로 주입
    buildSalt := os.Getenv("BUILD_GUID")
    mac := hmac.New(sha256.New, []byte(base))
    mac.Write([]byte(gameID + buildSalt))
    return hex.EncodeToString(mac.Sum(nil))
}

func (v *Validator) VerifySignature(req ScoreRequest, sig string) bool {
    // Replay Attack 방어 — 30초 허용
    if time.Now().Unix()-req.Timestamp > 30 {
        return false
    }
    secret := v.deriveSecretKey(req.GameID)
    payload := fmt.Sprintf("%s:%d:%.2f:%d",
        req.GameID, req.Score, req.PlayTime, req.Timestamp)
    mac := hmac.New(sha256.New, []byte(secret))
    mac.Write([]byte(payload))
    return hmac.Equal([]byte(hex.EncodeToString(mac.Sum(nil))), []byte(sig))
}
```

### 키 관리 원칙

| 항목 | 원칙 |
|------|------|
| 서버 | `HMAC_BASE_KEY` / `BUILD_GUID` Sealed Secret 으로 주입, 코드 하드코딩 금지 |
| 클라이언트 | 런타임 동적 생성, 단일 상수 문자열 사용 금지 |
| 키 노출 시 | 서버 환경변수 교체만으로 즉시 무효화 |
| 키 범위 | 게임별로 파생 → 키 하나 노출 시 피해 최소화 |

### 게임별 검증 규칙 (`config/games.yaml`)

새 미니게임 추가 시 **Go 코드 수정 금지**, yaml 만 추가한다.

```yaml
games:
  - id: "frog_catch_v1"
    min_play_seconds: 8
    max_score: 1000
    rate_limit_per_min: 10
  - id: "noodle_boil_v1"
    min_play_seconds: 15
    max_score: 500
    rate_limit_per_min: 5
```

서버는 점수 제출 시 이 yaml 의 `min_play_seconds`, `max_score`, `rate_limit_per_min` 으로 검증한다.

### 클라이언트 UX 원칙 (Non-blocking 점수 제출)

HMAC + 랭킹 호출에 1~2초가 소요될 수 있다. 화면이 멈추면 안 된다.

- 점수 제출 직후 **즉시** 결과 화면으로 전환
- 서버 응답은 백그라운드에서 처리
- 실패 시 **토스트만** 표시 (팝업 금지) — 게임 진행을 막지 않는다
- Unity: `UniTask` + `.Forget()` 패턴, `.Timeout(5s)` 항상 명시

---

## DB Connection Pool (pgx)

DB 연결 고갈은 Go 서버에서 가장 흔한 장애 원인. Phase 별 한도를 넘지 않도록 서버 단에서 제어한다.

```go
func NewDB(cfg Config) (*pgxpool.Pool, error) {
    poolCfg, _ := pgxpool.ParseConfig(cfg.DatabaseURL)
    poolCfg.MaxConns          = 10               // Phase 1 (Supabase 무료 한도 15)
    poolCfg.MinConns          = 2
    poolCfg.MaxConnLifetime   = 30 * time.Minute // DB 타임아웃보다 짧게
    poolCfg.MaxConnIdleTime   = 5 * time.Minute
    poolCfg.HealthCheckPeriod = 1 * time.Minute
    pool, err := pgxpool.NewWithConfig(context.Background(), poolCfg)
    if err != nil { return nil, err }
    if err := pool.Ping(context.Background()); err != nil {
        return nil, fmt.Errorf("db ping failed: %w", err)
    }
    return pool, nil
}
```

### Phase 별 권장값

| Phase | DB | MaxConns | 비고 |
|-------|----|----------|------|
| Phase 1 | Supabase 무료 | 10 | 한도 15 에서 여유 확보 |
| Phase 2 | Supabase 유료 | 25 | 한도 확장 후 조정 |
| Phase 3 | RDS t3.micro | 50 | 인스턴스 성능에 맞게 조정 |

> Pool 설정값은 환경변수로 주입해 코드 수정 없이 조정 가능하게 만든다.

---

## Worker Pool 패턴 — Analytics / Ranking 백그라운드 처리

`go` 키워드로 고루틴 무한 생성하면 트래픽 폭증 시 메모리 누수 / 서버 다운으로 이어진다.
**버퍼 채널 + 고정 Worker 수** 로 제어한다.

```go
type AnalyticsWorker struct {
    jobs    chan AnalyticsEvent
    workers int
}

func NewAnalyticsWorker(bufferSize, workerCount int) *AnalyticsWorker {
    w := &AnalyticsWorker{
        jobs:    make(chan AnalyticsEvent, bufferSize),
        workers: workerCount,
    }
    for i := 0; i < workerCount; i++ {
        go w.process()
    }
    return w
}

func (w *AnalyticsWorker) Enqueue(event AnalyticsEvent) {
    select {
    case w.jobs <- event:
    default:
        // 버퍼 꽉 찼을 때 드롭 — 지표 수집이 게임 서비스를 막으면 안 됨
        slog.Warn("analytics queue full, event dropped",
            "game_id", event.GameID)
    }
}
```

같은 패턴을 **Ranking Worker** 에도 적용 (배치 점수 반영, Exponential Backoff 재시도 최대 3회).

### 큐 깊이 메트릭 (커스텀 Prometheus Gauge)

Worker Pool 큐 적체는 Fiber 미들웨어가 잡지 못하므로 직접 등록한다.

```go
var (
    analyticsQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
        Name: "analytics_queue_depth",
        Help: "현재 Analytics Worker 큐에 쌓인 이벤트 수",
    })
    rankingQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
        Name: "ranking_queue_depth",
        Help: "현재 Ranking Worker 큐에 쌓인 작업 수",
    })
)

// Worker 루프 안에서 주기적으로 갱신
analyticsQueueDepth.Set(float64(len(w.jobs)))
```

---

## Graceful Shutdown

배포/재시작 시 처리 중 요청과 큐 잔여 작업이 유실되지 않도록 한다.

```go
func main() {
    app := fiber.New()
    analyticsWorker := NewAnalyticsWorker(1000, 5)
    rankingWorker   := NewRankingWorker(500, 3)

    go app.Listen(":8080")

    quit := make(chan os.Signal, 1)
    signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
    <-quit
    slog.Info("shutdown signal received, draining queues...")

    ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
    defer cancel()

    app.ShutdownWithContext(ctx)
    analyticsWorker.Shutdown(ctx)
    rankingWorker.Shutdown(ctx)
    slog.Info("server shutdown complete")
}
```

> k8s `terminationGracePeriodSeconds` 는 위 30초보다 크게(예: 45s) 설정한다.

---

## 구조화 로깅 (`log/slog`)

```go
// ❌ 금지 — 평문 로그, 분석 불가
log.Printf("score submit failed for user %s", userID)

// ✅ 필수 — JSON 구조화 로그, Loki 에서 필드 쿼리 가능
slog.Error("score submit failed",
    "user_id",   userID,
    "game_id",   req.GameID,
    "score",     req.Score,
    "play_time", req.PlayTime,
    "error",     err,
)
```

Go 1.21 표준 `log/slog` 로 시작. 성능이 필요한 시점에 `zap` 으로 전환.

---

## 관측성 (Observability)

### Fiber Prometheus 미들웨어

```go
import "github.com/ansrivas/fiberprometheus/v2"

func main() {
    app := fiber.New()
    prom := fiberprometheus.New("shortgeta-server")
    prom.RegisterAt(app, "/metrics")
    app.Use(prom.Middleware)
    // ... routes
    app.Listen(":8080")
}
```

### 수집 메트릭 목록

| 메트릭 | 출처 | 용도 |
|--------|------|------|
| `http_requests_total` | Fiber 미들웨어 | API 별 요청 수 + 상태코드 |
| `http_request_duration_seconds` (p50/p95/p99) | Fiber 미들웨어 | 응답시간 이상 탐지 |
| `go_goroutines` | Go runtime | Worker Pool 누수 감지 |
| `go_memstats_alloc_bytes` | Go runtime | 메모리 사용량 추이 |
| `db_pool_acquired_conns` | pgxpool 커스텀 | DB 연결 풀 사용률 |
| `analytics_queue_depth` | 커스텀 Gauge | Analytics 큐 적체 |
| `ranking_queue_depth` | 커스텀 Gauge | Ranking 큐 적체 |

### Grafana 알림 기준 (권장)

| 조건 | 알림 채널 |
|------|-----------|
| p99 응답시간 > 2초 | Slack `#dev-alerts` |
| 고루틴 수 > 500 | Slack `#dev-alerts` |
| DB 연결 사용률 > 80% | Slack `#dev-alerts` |
| Analytics/Ranking 큐 깊이 > 800 | Slack `#dev-alerts` |
| 5xx 에러율 > 1% | Slack `#prod-alerts` (즉시 대응) |

상세 인프라 측 설정은 `INFRA_PLAN.md` 의 Grafana Stack 절 참조.

---

## 코딩 규칙 (필수)

| # | 규칙 | 이유 |
|---|------|------|
| 1 | Fiber Handler 안에서는 `c.UserContext()` 사용. `c.Context()` 금지 | pgx 호환 (fasthttp.RequestCtx 미지원) |
| 2 | 환경변수 분기·Mock 코드는 **`main.go` 한 곳에서만** | 비즈니스 로직 오염 방지 |
| 3 | DB 쿼리는 **sqlc 생성 코드만** 사용. 인라인 SQL 금지 | 타입 안전성 |
| 4 | 고루틴 직접 생성 금지 → Worker Pool `Enqueue` 사용 | OOM/리소스 누수 방지 |
| 5 | 로그는 `slog` 만. `log.Printf` / `fmt.Println` 금지 | 구조화 검색 |
| 6 | 새 미니게임 추가 시 **`config/games.yaml` 만 수정**, Go 코드 변경 금지 | 운영 단순화 |
| 7 | DB 마이그레이션은 goose `Up` / `Down` 양방향 작성 | 롤백 가능성 |

---

## 개발 환경 — Mock 모드

서버를 클라이언트 / 외부 API 없이 독립 실행할 수 있도록 Mock 인터페이스를 제공한다.

```go
// pkg/steamapi/interface.go
type SteamAPI interface { VerifyTicket(ticket string) (string, error) }

// pkg/steamapi/mock.go
type MockSteamClient struct{ FixedSteamID string }
func (m *MockSteamClient) VerifyTicket(_ string) (string, error) { return m.FixedSteamID, nil }
```

`main.go` 에서만 분기:

```go
var steamClient steamapi.SteamAPI
if os.Getenv("STEAM_MOCK") == "true" {
    steamClient = &steamapi.MockSteamClient{FixedSteamID: "76561198000000001"}
} else {
    steamClient = steamapi.NewRealSteamClient(os.Getenv("STEAM_API_KEY"))
}
```

HMAC 검증도 동일 패턴으로 Mock 모드에서 우회 가능하게 설계.

---

## 관련 문서

| 파일 | 용도 |
|------|------|
| `shortgeta-plan-v1.3.html` | 마스터 계획서 |
| `PROJECT_PLAN.md` | 계획서 핵심 요약 |
| `GIT_STRATEGY.md` | 모노레포 구성 + 브랜치 전략 |
| `CLAUDE.md` | 코드 컨벤션 (API 엔드포인트 목록 포함) |
