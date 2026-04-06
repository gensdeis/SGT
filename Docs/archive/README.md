# Archive — 이전 계획 문서

이 디렉토리의 문서들은 **현재 정식 계획이 아니다**.

`shortgeta-plan-v1.3.html` 채택 이전의 설계 문서로,
풍부한 상세 설계(HMAC 동적 키 파생, Worker Pool, Sealed Secrets 마스터 키 백업, GHCR PAT, cert-manager, Loki 등)가 포함되어 있어
참조용으로 보존한다.

## 차이점

| 항목 | v3 (archive) | 현재 (v1.3 + monorepo) |
|------|--------------|------------------------|
| 프로젝트명 | "미니게임 프로젝트" (Steam 단독) | "숏게타 ShortGameTown" (Android 우선) |
| 레포 구조 | 멀티레포 4개 | 모노레포 (`client/`, `server/`, `ops-tool/`, `infra/`) |
| 브랜치 | `main` + `develop` | `main`/`dev`/`qa`/`real`/`hotfix`/`pre` |
| 백엔드 배포 | Fly.io + Supabase | k3s + ArgoCD |
| 인증 | Steam Ticket 단독 | 디바이스 토큰 + JWT (Phase 2 SSO) |

## 정식 문서 위치

- `Docs/PROJECT_PLAN.md` — 계획 핵심 요약
- `Docs/BACKEND_PLAN.md` — Go 백엔드 구현 계획
- `Docs/INFRA_PLAN.md` — 인프라 구현 계획
- `Docs/GIT_STRATEGY.md` — 모노레포 + 브랜치 전략
- `Docs/dev_guidelines.md` — 개발 지침서

## 흡수 완료 (2026-04-06)

v3의 핵심 설계는 정식 문서로 흡수되었다.

**BACKEND_PLAN.md 흡수 완료**
- [x] HMAC 동적 키 파생 전략 (런타임 조합 + 게임별 파생) + 코딩 규칙
- [x] Worker Pool 패턴 (Analytics / Ranking 큐 깊이 Gauge)
- [x] Graceful Shutdown 절차
- [x] pgx Pool Phase별 권장값 (10/25/50)
- [x] 구조화 로깅 (slog) 규칙
- [x] Prometheus 미들웨어 + 커스텀 메트릭
- [x] Non-blocking 점수 제출 UX 원칙
- [x] sqlc / goose / Fiber UserContext / DI / Mock 코딩 규칙

**INFRA_PLAN.md 흡수 완료**
- [x] Sealed Secrets 마스터 키 백업 절차 (필수)
- [x] GHCR Private 이미지 PAT 인증 (imagePullSecrets)
- [x] cert-manager + Let's Encrypt ClusterIssuer
- [x] Grafana + Loki + Prometheus 스택 (Phase 2)
- [x] Grafana 알림 기준
- [x] 장애 등급(P1/P2/P3) 및 대응 런북

> v3 원본은 참조용으로 보존하되, 모든 구현은 정식 문서를 따른다.
