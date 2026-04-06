# 미니게임 프로젝트 — Go 백엔드 서버 개발 계획서 v3

## 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 게임 장르 | B급 감성 미니게임 컬렉션 (숏게임) |
| 클라이언트 | Unity |
| 백엔드 언어 | Go 1.21+ |
| 출시 플랫폼 | Steam (패키지 저가 판매) |
| 개발 인원 | 1~2인 |
| 목표 기간 | 12~16주 |

---

## 기술 스택

| 역할 | 선택 |
|---|---|
| 언어 | Go 1.21+ |
| 웹 프레임워크 | Fiber |
| DB | PostgreSQL |
| DB 드라이버 | pgx v5 (Connection Pool 내장) |
| ORM/쿼리 | sqlc (SQL → Go 자동 생성) |
| 마이그레이션 | goose |
| 캐시 / Rate Limit | Redis (Upstash) |
| 인증 | Steam Ticket 검증 (별도 OAuth 불필요) |
| 메트릭 | Prometheus + Fiber 미들웨어 |
| 로깅 | log/slog (JSON 구조화 로그) |
| 배포 | Fly.io (출시 첫날부터 운영) |
| CI/CD | GitHub Actions |
| 모니터링 | Grafana Cloud 무료 플랜 |

---

## 인증 설계

Steam 클라이언트가 로그인을 전담하기 때문에 별도 OAuth, 회원가입, 비밀번호 관리가 전혀 필요 없다.

### 인증 흐름

```
Unity 클라이언트
  └─ SteamUser.GetAuthTicketForWebApi() 로 Ticket 발급
        ↓
Go 서버 /auth/login
  └─ Steam Web API 에 Ticket 검증 요청
  └─ SteamID 반환받아 세션 발급 (Redis TTL 24h)
  └─ 최초 로그인이면 users 테이블에 자동 생성
```

### 유저 식별자

```
SteamID (64bit 고유값) 를 DB PK 로 그대로 사용
→ 중복 가입, 탈퇴 처리 전부 불필요
```

### 핵심 코드 구조

```go
func (h *AuthHandler) Login(c *fiber.Ctx) error {
    ticket := c.Get("X-Steam-Ticket")

    steamID, err := h.steamClient.VerifyTicket(ticket)
    if err != nil {
        return c.Status(401).JSON(fiber.Map{"error": "invalid ticket"})
    }

    user, err := h.userRepo.FindOrCreate(c.Context(), steamID)
    sessionToken := h.session.Create(steamID)

    return c.JSON(fiber.Map{"token": sessionToken})
}
```

---

## DB 연결 관리 (Connection Pooling)

DB 연결 수 고갈은 Go 서버에서 가장 흔히 발생하는 장애 원인 중 하나다.
특히 Supabase 무료 티어는 동시 연결이 15개로 제한되어 있어 서버 단에서 반드시 제어해야 한다.

### pgx Pool 설정

```go
func NewDB(cfg Config) (*pgxpool.Pool, error) {
    poolCfg, err := pgxpool.ParseConfig(cfg.DatabaseURL)
    if err != nil {
        return nil, err
    }

    // Phase 1 (Supabase 무료): 여유 있게 잡아야 함
    // Supabase 무료 티어 최대 15개 → 서버가 절대 초과하지 않도록 설정
    poolCfg.MaxConns = 10                          // 최대 동시 연결 수
    poolCfg.MinConns = 2                           // 최소 유지 연결 수
    poolCfg.MaxConnLifetime = 30 * time.Minute     // 연결 최대 수명 (DB 측 타임아웃보다 짧게)
    poolCfg.MaxConnIdleTime = 5 * time.Minute      // 유휴 연결 반환 시간
    poolCfg.HealthCheckPeriod = 1 * time.Minute    // 죽은 연결 주기적 정리

    pool, err := pgxpool.NewWithConfig(context.Background(), poolCfg)
    if err != nil {
        return nil, err
    }

    // 서버 시작 시 연결 상태 확인
    if err := pool.Ping(context.Background()); err != nil {
        return nil, fmt.Errorf("db ping failed: %w", err)
    }

    return pool, nil
}
```

### Phase 별 Pool 설정 권장값

| Phase | 인프라 | MaxConns | 비고 |
|---|---|---|---|
| Phase 1 | Supabase 무료 | 10 | 무료 티어 한도 15개에서 여유 확보 |
| Phase 2 | Supabase 유료 | 25 | 한도 확장 후 조정 |
| Phase 3 | RDS t3.micro | 50 | 인스턴스 성능에 맞게 조정 |

> Pool 설정값은 `games.yaml` 과 마찬가지로 환경변수로 주입해 코드 수정 없이 조정 가능하게 만든다.

---

## 어뷰징 방어 설계

### 검증 레이어 구조 (3단계)

```
레이어 1 (클라이언트)  — 메모리 변조 방어
레이어 2 (전송 구간)   — HMAC Signature 무결성 검증
레이어 3 (서버)        — 시간/점수 범위 + Rate Limit
```

세 레이어가 모두 통과해야 Steam Leaderboard 에 점수가 반영된다.

---

### 레이어 1 — 클라이언트 메모리 변조 방어 (Unity)

Cheat Engine 등 메모리 변조 툴을 통한 점수/플레이 시간 변수 조작을 방어한다.

**적용 방안**

- 점수·시간 변수를 평문으로 저장하지 않고 XOR 등 경량 난독화로 래핑
- 유료 안티치트 에셋 또는 오픈소스 변수 보호 라이브러리 적용 검토
- 빌드 시 IL2CPP + Code Stripping 으로 역공학 난이도 상향

> 클라이언트 방어는 완벽하지 않다. 레이어 2, 3 서버 검증이 반드시 함께 있어야 의미가 있다.

---

### 레이어 2 — HMAC Payload Signature (전송 무결성)

클라이언트를 거치지 않고 Postman 등으로 API 를 직접 호출하는 Spoofing 공격을 방어한다.

#### Secret Key 동적 생성 전략

Secret Key 를 코드에 하드코딩하면 IL2CPP 를 써도 숙련된 공격자에게는 결국 추출된다.
**정적 분석을 어렵게 만들기 위해 런타임에 여러 조각을 조합해 동적으로 생성**한다.

```csharp
// ❌ 취약한 방식 — 정적 분석으로 즉시 추출 가능
const string SECRET_KEY = "my-secret-key-1234";

// ✅ 강화된 방식 — 런타임 조합 + 동적 파생
private static string BuildSecretKey(string gameId) {
    // 1. 여러 조각으로 분산 (정적 분석 난이도 상승)
    string[] parts = { "m1n1", "G4me", "S3cr" };
    string base = string.Join("-", parts);

    // 2. GameID + 빌드 시 주입된 Salt 를 조합해 게임마다 다른 키 파생
    //    → 키 하나가 노출돼도 다른 게임에 영향 없음
    string salt = Application.buildGUID; // 빌드마다 고유값
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(base));
    byte[] derived = hmac.ComputeHash(Encoding.UTF8.GetBytes(gameId + salt));
    return Convert.ToHexString(derived).ToLower();
}

string CreateSignature(string gameId, int score, float playTime, long timestamp) {
    string secretKey = BuildSecretKey(gameId);
    string payload = $"{gameId}:{score}:{playTime:.00}:{timestamp}";
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash).ToLower();
}
```

**서버 (Go) — 동일한 파생 로직 구현**

```go
func (v *Validator) deriveSecretKey(gameID string) string {
    base := os.Getenv("HMAC_BASE_KEY") // 서버는 환경변수로 관리
    buildSalt := os.Getenv("BUILD_GUID")

    mac := hmac.New(sha256.New, []byte(base))
    mac.Write([]byte(gameID + buildSalt))
    return hex.EncodeToString(mac.Sum(nil))
}

func (v *Validator) VerifySignature(req ScoreRequest, signature string) bool {
    secretKey := v.deriveSecretKey(req.GameID)
    payload := fmt.Sprintf("%s:%d:%.2f:%d",
        req.GameID, req.Score, req.PlayTime, req.Timestamp)

    mac := hmac.New(sha256.New, []byte(secretKey))
    mac.Write([]byte(payload))
    expected := hex.EncodeToString(mac.Sum(nil))

    // 타임스탬프 허용 오차 30초 — Replay Attack 방어
    if time.Now().Unix()-req.Timestamp > 30 {
        return false
    }

    return hmac.Equal([]byte(expected), []byte(signature))
}
```

**Secret Key 관리 원칙**

| 항목 | 원칙 |
|---|---|
| 서버 | Fly.io 환경변수로 주입, 코드 하드코딩 금지 |
| 클라이언트 | 런타임 동적 생성, 단일 상수 문자열 사용 금지 |
| 키 노출 시 | 서버 환경변수 교체만으로 즉시 무효화 |
| 키 범위 | 게임별로 파생 → 키 하나 노출 시 피해 최소화 |

---

### 레이어 3 — 서버사이드 검증 (yaml 기반)

```go
func (v *Validator) Check(req ScoreRequest, signature string) error {
    if !v.VerifySignature(req, signature) {
        return ErrInvalidSignature
    }
    cfg := v.getGameConfig(req.GameID)
    if req.PlayTime < float64(cfg.MinPlaySeconds) {
        return ErrPlayTimeTooShort
    }
    if req.Score > cfg.MaxScore {
        return ErrScoreOutOfRange
    }
    if err := v.rateLimit(req.SteamID, req.GameID); err != nil {
        return ErrRateLimitExceeded
    }
    return nil
}
```

**게임별 설정 (yaml)**

```yaml
# config/games.yaml
games:
  - id: "mole_whack"
    min_play_seconds: 8
    max_score: 1000
    rate_limit_per_min: 10

  - id: "timing_jump"
    min_play_seconds: 15
    max_score: 500
    rate_limit_per_min: 5
```

---

## 클라이언트 UX — Non-blocking 점수 제출

HMAC 검증과 Steam Leaderboard API 호출이 완료되기까지 1~2초가 소요될 수 있다.
이 시간 동안 화면이 멈춘 것처럼 느껴지면 게임 체감 품질이 크게 떨어진다.

### 낙관적 UI (Optimistic UI) 패턴

```
[기존 Blocking 방식]
점수 제출 → 서버 응답 대기 (1~2초 화면 멈춤) → 결과 화면

[개선된 Non-blocking 방식]
점수 제출 → 즉시 결과 화면 전환 → 서버 응답을 백그라운드 처리
              └─ 성공: 랭킹 UI 갱신
              └─ 실패: "점수 저장 실패" 토스트 메시지 (비침습적)
```

**Unity C# 구현 방향**

```csharp
async void SubmitScoreAndProceed(int score, float playTime) {
    // 1. 즉시 결과 화면으로 전환 (유저는 지연을 느끼지 못함)
    SceneManager.LoadScene("ResultScene");

    // 2. 서버 통신은 백그라운드에서 처리
    try {
        bool success = await scoreApi.SubmitAsync(score, playTime);
        if (success) {
            // 랭킹 UI 갱신
            LeaderboardUI.Refresh();
        } else {
            // 비침습적 실패 안내 (게임 흐름을 막지 않음)
            ToastUI.Show("점수 저장에 실패했습니다.");
        }
    } catch (Exception e) {
        ToastUI.Show("네트워크 오류가 발생했습니다.");
    }
}
```

> 서버 응답 실패가 게임 진행 자체를 막아서는 안 된다. 점수 저장 실패는 유저 경험을 방해하지 않는 수준으로만 안내한다.

---

## 리더보드 설계

Steam Leaderboard API 를 클라이언트가 직접 호출하면 점수 조작이 가능하므로 **서버에서 Publisher API Key 로 호출**하는 구조를 사용한다.

### Steam API Rate Limit 대비

동접자가 몰릴 때 Steam Publisher API 의 Rate Limit 을 초과할 수 있다. 로컬 큐잉으로 대비한다.

```go
type LeaderboardWorker struct {
    queue  chan ScoreJob
    client *SteamClient
}

func (w *LeaderboardWorker) Start(ctx context.Context) {
    for {
        select {
        case job := <-w.queue:
            err := w.client.SetLeaderboardScore(job)
            if err != nil {
                // Exponential Backoff 재시도 (최대 3회)
                w.retry(job, 3)
            }
        case <-ctx.Done():
            w.drainAndClose()
            return
        }
    }
}
```

Steam 이 제공하는 기능은 직접 구현하지 않는다.

| 기능 | 담당 |
|---|---|
| 리더보드 | Steam Leaderboard |
| 업적 | Steam Achievements |
| 세이브 동기화 | Steam Cloud Save |
| DLC 판매 | Steam Store |
| 업데이트 배포 | Steam Depot |

---

## Go 백엔드 안정성 설계

### 비동기 지표 수집 — Worker Pool 패턴

단순 `go` 키워드로 고루틴을 무한 생성하면 트래픽 폭증 시 메모리 누수나 서버 다운으로 이어진다.
버퍼 채널 + 고정 Worker 수 로 제어한다.

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
        // 버퍼 꽉 찼을 때 드롭 (지표 수집이 게임 서비스를 막으면 안 됨)
        slog.Warn("analytics queue full, event dropped",
            "game_id", event.GameID)
    }
}
```

---

### Graceful Shutdown

배포/재시작 시 처리 중이던 요청과 큐 잔여 작업이 유실되지 않도록 한다.

```go
func main() {
    app := fiber.New()
    analyticsWorker := NewAnalyticsWorker(1000, 5)
    lbWorker := NewLeaderboardWorker(500, 3)

    go app.Listen(":8080")

    quit := make(chan os.Signal, 1)
    signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
    <-quit

    slog.Info("shutdown signal received, draining queues...")

    ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
    defer cancel()

    app.ShutdownWithContext(ctx)
    analyticsWorker.Shutdown(ctx)
    lbWorker.Shutdown(ctx)

    slog.Info("server shutdown complete")
}
```

---

### 구조화 로깅 (Structured Logging)

```go
// ❌ 평문 로그 — 분석 불가
log.Printf("score submit failed for user %s", steamID)

// ✅ 구조화 로그 — Grafana 에서 필드별 쿼리 가능
slog.Error("score submit failed",
    "steam_id",  steamID,
    "game_id",   req.GameID,
    "score",     req.Score,
    "play_time", req.PlayTime,
    "error",     err,
)
```

Go 1.21 표준 `log/slog` 로 시작하고, 성능이 필요한 시점에 `zap` 으로 전환한다.

---

## 관측성 (Observability) 설계

로그만으로는 서버 전반 상태를 한눈에 파악하기 어렵다.
Prometheus 메트릭을 수집해 Grafana 대시보드에서 시각화한다.

### Fiber Prometheus 미들웨어 적용

Fiber 에 내장된 미들웨어로 코드 몇 줄만으로 핵심 메트릭을 수집할 수 있다.

```go
import "github.com/ansrivas/fiberprometheus/v2"

func main() {
    app := fiber.New()

    // Prometheus 미들웨어 등록 — 이 한 블록으로 기본 메트릭 수집 시작
    prometheus := fiberprometheus.New("minigame-server")
    prometheus.RegisterAt(app, "/metrics")
    app.Use(prometheus.Middleware)

    // 라우터 등록
    app.Post("/score/submit", scoreHandler.Submit)
    app.Post("/auth/login", authHandler.Login)

    app.Listen(":8080")
}
```

### 수집 메트릭 목록

| 메트릭 | 용도 |
|---|---|
| `http_requests_total` | API 별 요청 수 + 상태코드 분포 |
| `http_request_duration_seconds` (p95, p99) | API 응답 시간 이상 탐지 |
| `go_goroutines` | 고루틴 수 — Worker Pool 누수 감지 |
| `go_memstats_alloc_bytes` | 메모리 사용량 추이 |
| `db_pool_acquired_conns` | DB 연결 풀 사용률 |
| `analytics_queue_depth` | Analytics 큐 적체 여부 |
| `leaderboard_queue_depth` | Leaderboard 큐 적체 여부 |

### 커스텀 메트릭 (큐 깊이 모니터링)

Fiber 미들웨어가 제공하지 않는 Worker Pool 큐 상태는 직접 등록한다.

```go
var (
    analyticsQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
        Name: "analytics_queue_depth",
        Help: "현재 Analytics Worker 큐에 쌓인 이벤트 수",
    })
    leaderboardQueueDepth = promauto.NewGauge(prometheus.GaugeOpts{
        Name: "leaderboard_queue_depth",
        Help: "현재 Leaderboard Worker 큐에 쌓인 작업 수",
    })
)

// Worker 루프 안에서 주기적으로 갱신
analyticsQueueDepth.Set(float64(len(w.jobs)))
```

### Grafana 알림 기준 (권장)

| 조건 | 알림 |
|---|---|
| p99 응답시간 > 2초 | Slack 알림 |
| 고루틴 수 > 500 | Slack 알림 |
| DB 연결 사용률 > 80% | Slack 알림 |
| Analytics 큐 깊이 > 800 | Slack 알림 |
| 5xx 에러율 > 1% | PagerDuty (즉시 대응) |

---

## 지표 수집 설계

### 기본 지표

- 게임 시작 / 종료 / 클리어
- 게임별 클리어율
- 평균 점수 / 평균 시도 수

### 이탈 구간 분석 지표 (Funnel)

미니게임에서 "어느 순간에 짜증나서 끄는가"를 파악하는 것이 밸런싱의 핵심이다.

| 이벤트 | 용도 |
|---|---|
| `game_start` | 진입 수 |
| `game_death` + `obstacle_id` | 특정 구간 사망 집중 여부 파악 |
| `game_retry` | 재도전 횟수 → 적당한 난이도 지표 |
| `game_quit` + `elapsed_time` | 이탈 구간 파악 |
| `game_clear` | 최종 클리어율 |

```go
type GameEvent struct {
    SteamID    string    `json:"steam_id"`
    GameID     string    `json:"game_id"`
    EventType  string    `json:"event_type"`
    ObstacleID string    `json:"obstacle_id,omitempty"`
    ElapsedMs  int64     `json:"elapsed_ms"`
    Score      int       `json:"score,omitempty"`
    Timestamp  time.Time `json:"timestamp"`
}
```

### 밸런싱 활용 기준 (예시)

| 지표 | 기준 | 액션 |
|---|---|---|
| 클리어율 | 80% 이상 | 난이도 상향 |
| 클리어율 | 20% 이하 | 난이도 하향 |
| 특정 obstacle 사망 집중 | 전체 사망의 60% 이상 | 해당 구간 조정 |
| 재도전 0회 quit | 전체 quit 의 50% 이상 | 첫 인상 개선 필요 |

---

## 프로젝트 디렉터리 구조

```
go-minigame/
├── cmd/server/             # main.go 진입점 + Graceful Shutdown
├── internal/
│   ├── auth/               # Steam 인증, 세션 관리
│   ├── score/              # 점수 검증 (HMAC + yaml 규칙)
│   ├── leaderboard/        # Steam Leaderboard Worker Pool
│   ├── analytics/          # 지표 수집 Worker Pool
│   └── metrics/            # Prometheus 커스텀 메트릭
├── pkg/
│   ├── steamapi/           # Steam Web API 클라이언트
│   ├── hmac/               # Signature 생성/검증 유틸
│   └── db/                 # pgx Pool 초기화 + 설정
├── db/
│   ├── queries/            # sqlc 용 SQL 쿼리
│   └── migrations/         # goose 마이그레이션
└── config/
    └── games.yaml          # 게임별 검증 설정값
```

---

## 인프라 단계별 플랜

> Phase 1 부터 Go 서버를 반드시 운영한다. 서버사이드 어뷰징 검증과 Steam Leaderboard 서버사이드 호출은 출시 첫날부터 필요하다.

### Phase 1 — 출시 초기 (월 $10~15)

| 구성 요소 | 내용 |
|---|---|
| 서버 | Fly.io 최소 인스턴스 1개 (출시 첫날부터 필수) |
| DB | Supabase 무료 플랜 (연결 15개 한도 → Pool MaxConns=10) |
| 캐시 | Upstash Redis 무료 플랜 |
| 랭킹 | Steam Leaderboard (서버사이드 호출) |
| 배포 | Steam Depot + Fly.io |
| 분석 | Prometheus + Grafana Cloud 무료 플랜 |

### Phase 2 — 유저 1,000명 이상 (월 $30~60)

| 구성 요소 | 내용 |
|---|---|
| 서버 | Fly.io 인스턴스 스케일업 |
| DB | Supabase 유료 전환 (연결 한도 확장 → Pool MaxConns=25) |
| 캐시 | Upstash Redis 사용량 기반 |
| CDN | Cloudflare 무료 플랜 |

### Phase 3 — 유저 10,000명 이상 (월 $100~300)

| 구성 요소 | 내용 |
|---|---|
| 서버 | AWS ECS Fargate (오토스케일링) |
| DB | RDS PostgreSQL t3.micro (Pool MaxConns=50) |
| 캐시 | ElastiCache Redis |
| CDN | CloudFront |
| 모니터링 | Grafana Cloud 유료 전환 검토 |

---

## 개발 일정

| 주차 | 작업 내용 |
|---|---|
| 1주차 | Go 프로젝트 구조 + pgx Pool 설정 + Docker 환경 구성 |
| 2주차 | Steam Ticket 검증 API 구현 |
| 3주차 | 세션 발급 + 유저 테이블 + 기본 인증 완성 |
| 4주차 | HMAC 동적 키 파생 구현 (Go 서버 + Unity C# 동시) |
| 5주차 | yaml 기반 점수/시간 검증 + Redis Rate Limit |
| 6주차 | Steam Leaderboard Worker Pool + Exponential Backoff |
| 7주차 | Analytics Worker Pool + Funnel 이벤트 수집 |
| 8주차 | Graceful Shutdown + 구조화 로깅 적용 |
| 9주차 | Prometheus 미들웨어 + 커스텀 메트릭 + Grafana 대시보드 |
| 10주차 | Fly.io 배포 + GitHub Actions CI/CD |
| 11주차 | 부하 테스트 + Grafana 알림 기준 세팅 |
| 12주차 | Unity 클라이언트 Non-blocking UX + 메모리 난독화 |

**총 예상 기간: 12~16주**

---

## 개발 환경 팁

Steam 클라이언트 없이 서버를 독립적으로 테스트할 수 있도록 **Steam Ticket Mock 모드**를 개발 환경에서 지원한다.
HMAC Signature 도 동일하게 Mock 모드에서 우회하도록 설정해 로컬 개발 편의성을 확보한다.

```go
func (c *SteamClient) VerifyTicket(ticket string) (string, error) {
    if os.Getenv("STEAM_MOCK") == "true" {
        return "76561198000000001", nil
    }
    return c.callSteamAuthAPI(ticket)
}
```
