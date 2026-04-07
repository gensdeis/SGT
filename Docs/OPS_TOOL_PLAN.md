# 숏게타 운영툴 (ops-tool) 계획

## 스택
- Next.js 15 (App Router) + TypeScript + Tailwind CSS
- 인증: 운영자 ID/PW + admin JWT (서버 별도 secret)
- 배포: kind 클러스터 + ArgoCD (Iter 4c)

## 디렉토리
```
ops-tool/
├── app/                # App Router pages
├── lib/api.ts          # /v1/admin/* fetch wrapper
├── Dockerfile
├── package.json
└── ...
```

## 서버 영역
- migration `20260409120001_admin.sql`: admin_users, users.banned, notices
- `internal/admin/{auth,middleware,handler}.go`
- 비밀번호: sha256+salt (Iter 4a 단순화 — 후속 bcrypt/argon2)
- admin JWT: 별도 secret (`ADMIN_JWT_SECRET`), 12h TTL
- 시드: `ADMIN_BOOTSTRAP_LOGIN/PASSWORD` env 로 idempotent insert

## 단계별 계획

### Iter 4a — 로그인 + 유저 검색 (본 PR)
- 서버: admin login + /v1/admin/users 검색만
- 클라: 로그인 페이지 + 검색 페이지

### Iter 4b — CRUD + 대시보드
- 코인 조정, 게임 카탈로그 편집
- 랭킹/세션 read-only 대시보드

### Iter 4c — 공지/밴/푸시 + 인프라 배포
- notices CRUD, ban 토글, push stub
- k8s manifest + ArgoCD application
- Traefik basic auth + IP whitelist

## 환경변수
| 키 | 기본값 | 설명 |
|---|---|---|
| `ADMIN_JWT_SECRET` | `JWT_SECRET + "-admin"` | admin 토큰 서명 |
| `ADMIN_JWT_TTL_HOURS` | 12 | TTL |
| `ADMIN_BOOTSTRAP_LOGIN` | (empty=skip) | 시드 admin login |
| `ADMIN_BOOTSTRAP_PASSWORD` | (empty=skip) | 시드 admin password |
| `NEXT_PUBLIC_API_BASE` | `http://localhost:18081/v1` | ops-tool → server |

## 한계 (Iter 4a)
- 토큰 저장은 localStorage (XSS 위험 — 후속 httpOnly cookie)
- sha256+salt < bcrypt
- CORS 와일드카드 (dev 만)
- 페이지 1개 (로그인 + 검색만)
