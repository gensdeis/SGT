# 숏게타 — Go 게임 서버 (`server/`)

`Docs/BACKEND_PLAN.md` Phase 1 구현체.
이미지: `ghcr.io/gensdeis/shortgeta-server:<short_sha>` (CI 자동 push).

## 빠른 시작 (로컬)

```bash
# 1. 환경변수
cp .env.example .env.local
set -a && source .env.local && set +a

# 2. 의존성 (PostgreSQL, Redis, Adminer)
docker compose up -d

# 3. DB 마이그레이션
goose -dir db/migrations postgres "$DATABASE_URL" up
# 또는 (goose 미설치 시) 서버가 시작 시 자동 마이그레이션 — 현재는 비활성. 추후 옵션화 예정.

# 4. 서버 실행
go run ./cmd/api
# → http://localhost:8080
```

Adminer: http://localhost:8081  (서버 `postgres`, 사용자 `shortgeta`, 비밀번호 `devpass`, DB `shortgeta_dev`)

## API 엔드포인트 (Phase 1)

| Method | Path | 설명 | 인증 |
|---|---|---|---|
| GET | `/health` | liveness | - |
| GET | `/ready` | readiness (DB ping) | - |
| GET | `/metrics` | Prometheus | - |
| POST | `/v1/auth/device` | 디바이스 토큰 → JWT | - |
| GET | `/v1/games` | 게임 목록 (?tags=) | JWT |
| GET | `/v1/games/:id` | 게임 상세 | JWT |
| POST | `/v1/sessions` | 세션 시작 (추천 큐) | JWT |
| POST | `/v1/sessions/:id/end` | 세션 종료 + 점수 제출 | JWT |
| GET | `/v1/rankings/global` | 글로벌 랭킹 | JWT |
| GET | `/v1/rankings/:gameId` | 게임별 랭킹 | JWT |
| POST | `/v1/analytics/event` | 플레이 이벤트 | JWT |
| POST | `/v1/purchases/verify` | 광고제거 영수증 검증 | JWT |

## 환경변수

`.env.example` 참조. 모든 값은 `internal/config/config.go` 에서 로드.

| 변수 | 기본값 | 비고 |
|---|---|---|
| `PORT` | 8080 | HTTP 포트 |
| `LOG_LEVEL` | info | debug/info/warn/error |
| `DATABASE_URL` | (필수) | postgres connection string |
| `DB_MAX_CONNS` | 10 | Phase 1 Supabase 무료 한도 15 보호 |
| `REDIS_URL` | (필수) | rate limit + cache |
| `JWT_SECRET` | (필수) | HS256 signing key |
| `JWT_TTL_HOURS` | 720 | 30일 |
| `HMAC_BASE_KEY` | (필수) | 점수 서명 베이스 키 |
| `BUILD_GUID` | (필수) | HMAC 파생 salt |
| `HMAC_REPLAY_WINDOW_SEC` | 30 | replay attack 윈도우 |
| `DEV_MOCK_RECEIPT` | false | true 면 영수증 검증 mock |
| `GAMES_CONFIG_PATH` | ./config/games.yaml | 게임 메타 yaml |

## 디렉토리 구조

```
server/
├── cmd/
│   ├── api/              # main.go — DI 조립 + Graceful Shutdown
│   └── worker/           # 배치/리셋 잡 (스켈레톤)
├── internal/             # 외부 import 불가
│   ├── analytics/        # 이벤트 수집 + Worker Pool
│   ├── anticheat/        # HMAC + games.yaml 검증 + rate limit
│   ├── auth/             # 디바이스 토큰 + JWT
│   ├── config/           # env + games.yaml 로딩
│   ├── dda/              # SR 기반 강도 조절
│   ├── game/             # Game Registry
│   ├── metrics/          # Prometheus 커스텀 게이지
│   ├── purchase/         # Play Billing 영수증 검증
│   ├── ranking/          # 게임별/글로벌 랭킹 + Worker Pool
│   ├── ratelimit/        # Redis 토큰 버킷
│   ├── session/          # 세션 + 추천기
│   └── storage/sqlc/     # sqlc 생성 코드 (커밋됨)
├── pkg/                  # 외부 import 가능
│   ├── db/               # pgxpool 초기화
│   └── hmac/             # 동적 키 파생 + 검증
├── db/
│   ├── migrations/       # goose
│   └── queries/          # sqlc SQL
├── config/
│   └── games.yaml        # 게임별 검증 설정 (코드 변경 없이 게임 추가)
├── deploy/               # k8s overlays — ArgoCD 추적 (server-dev)
│   ├── base/
│   └── overlays/{dev,qa,real}/
├── docker-compose.yml    # 로컬 PG/Redis/Adminer
├── Dockerfile            # multi-stage, alpine, non-root
├── Makefile
├── go.mod
└── README.md
```

## 코딩 규칙

`Docs/BACKEND_PLAN.md` §"코딩 규칙" 7개 항목 준수. 핵심:
1. Fiber Handler 안에서 `c.UserContext()` 사용 (`c.Context()` 금지)
2. 환경변수 분기 / Mock 코드는 **`cmd/api/main.go` 에서만**
3. DB 쿼리는 sqlc 생성 코드만, 인라인 SQL 금지
4. 고루틴 직접 생성 금지 → Worker Pool `Enqueue`
5. 로그는 `slog` 만
6. 새 게임 추가는 `config/games.yaml` 만 수정
7. goose 마이그레이션은 Up/Down 양방향

## 테스트

```bash
go test ./...
go test ./... -race -coverprofile=coverage.out
go tool cover -html=coverage.out
```

## Docker 빌드 + kind 로드

```bash
docker build -t shortgeta-server:dev .
kind load docker-image shortgeta-server:dev --name shortgeta
# ArgoCD shortgeta-server-dev Application 이 자동으로 sync
```
