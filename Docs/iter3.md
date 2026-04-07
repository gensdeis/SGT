# Iter 3 — 랭킹 UI + 프로필 + 데일리 미션 + 공유 보상

## 범위
- 랭킹: 서버 reuse, 클라 결과 패널에 "🏆 랭킹 보기" 버튼
- 프로필: `users` 컬럼 확장 (nickname, avatar_id, coins) + `GET/PATCH /v1/me`
- 데일리 미션: `daily_missions` 테이블 + `GET /v1/missions/today`, `POST /v1/missions/claim`
- 공유 보상: `share_rewards` 테이블 + `POST /v1/share/claim` (일 1회)
- 코인: `users.coins` 컬럼 — 미션/공유 보상이 직접 update

## 신규 / 수정 파일

**서버**
- `db/migrations/20260408120001_iter3.sql`
- `internal/storage/{profile.go,missions.go,share.go}` — raw pgx (sqlc 우회)
- `internal/profile/handler.go`
- `internal/missions/{defs.go,service.go,handler.go}`
- `internal/share/{service.go,handler.go}`
- `cmd/api/main.go` — 모듈 wiring
- `internal/session/service.go` — `MissionHook` 인터페이스 + 호출

**클라**
- `Network/{ProfileApi,MissionsApi,ShareApi}.cs`
- `Network/Models.cs` — DTO 추가
- `UI/Mobile/BootstrapController.cs` — 코인 표시, 미션 버튼, 랭킹 버튼, 공유 보상 hook

## 미션 정의 (코드 상수)
| ID | 타이틀 | 목표 | 보상 |
|---|---|---|---|
| play_3_games | 게임 3판 플레이 | 3 | 10🪙 |
| clear_1_game | 게임 1판 클리어 | 1 | 15🪙 |
| play_total_60s | 총 60초 플레이 | 60 | 5🪙 |

## 데이터 흐름
1. session.End → missions.OnSessionResult (best-effort, 트랜잭션 분리)
2. play_3_games += 1, clear 면 clear_1_game += 1, play_total_60s += seconds
3. progress >= target 시 completed_at set
4. 클라가 /v1/missions/claim → claimed_at set + coins 증가

## 검증
- 서버: `sqlc generate` 불필요 (raw pgx). `go vet ./...`, `go build ./...`
- curl:
  ```
  POST /v1/auth/device {device_id}
  GET  /v1/me
  POST /v1/me {nickname:"테스터",avatar_id:0}
  GET  /v1/missions/today
  POST /v1/missions/claim {mission_id:"play_3_games"}
  POST /v1/share/claim {platform:"twitter"}
  ```
- 클라: Editor ▶ Play → 콘솔 `[Profile] me loaded`, 홈 우상단 미션 버튼

## 알려진 한계
- raw pgx 사용 → "sqlc only" 규칙 부분 위반 (Iter 3 신규 테이블만)
- 미션 hook 트랜잭션 분리 → race 상황에서 progress 이중 update 가능
- 공유 보상이 실 공유 성공 여부 확인 없이 즉시 claim
- 닉네임 UI 변경 화면 없음 (API 만)
- 프로필 자동 로드 실패 시 코인 표시 미노출

## 후속 (Iter 3.5+)
- sqlc 정식 재생성
- 닉네임 변경 모달
- 공유 영수증 (서명) 검증
- wallet 모듈 (트랜잭션 history)
