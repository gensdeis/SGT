# 숏게타 — Go 백엔드 구현 계획

> 계획서 v1.3 (`shortgeta-plan-v1.3.html`) 기준.
> Phase별 로드맵과 일치하도록 백엔드 범위를 정의한다.

---

## 기술 스택

| 항목 | 선택 |
|------|------|
| 언어 | Go |
| 전송 | REST (JSON) — 내부 이벤트는 비동기 큐 검토 |
| 인프라 | k3s + ArgoCD, GitHub Actions CI/CD, Traefik, Sealed Secrets |
| 인증 | 디바이스 토큰 + JWT (Phase 2부터 SSO 검토) |
| Anti-cheat | HMAC 기반 점수 서명 (계획서 v1.3 §11) |
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

- [ ] Game Registry API (오리지널 게임 등록)
- [ ] Ranking API — 게임별 + 글로벌 (핵심)
- [ ] Analytics API — 이벤트 수집 + 태그 가중치 입력
- [ ] DDA 기초 로직 (서버 측 강도 태그 산출)
- [ ] HMAC 점수 서명 검증 (Anti-cheat 기반)
- [ ] k3s + ArgoCD 배포 파이프라인
- [ ] 디바이스 인증 + 광고제거 영수증 검증($2.99)

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
│   ├── api/              # REST API 서버
│   └── worker/           # 배치/리셋 잡
├── internal/
│   ├── game/             # Game Registry 도메인
│   ├── session/          # Session / Ranking
│   ├── analytics/        # Analytics 이벤트
│   ├── creator/          # Creator (Phase 3)
│   ├── dda/              # 강도 조절 로직
│   └── anticheat/        # HMAC 검증
├── migrations/
└── deploy/               # k8s 매니페스트 (infra/와 분리)
```

---

## 관련 문서

| 파일 | 용도 |
|------|------|
| `shortgeta-plan-v1.3.html` | 마스터 계획서 |
| `PROJECT_PLAN.md` | 계획서 핵심 요약 |
| `GIT_STRATEGY.md` | 모노레포 구성 + 브랜치 전략 |
| `CLAUDE.md` | 코드 컨벤션 (API 엔드포인트 목록 포함) |
