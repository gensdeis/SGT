# 숏게타 운영툴 (ops-tool)

Next.js 15 App Router + TS + Tailwind 기반 admin 대시보드.

## 로컬 실행

```bash
cd ops-tool
npm install
# 서버 주소 (선택, 기본 http://localhost:18081/v1)
echo "NEXT_PUBLIC_API_BASE=http://localhost:18081/v1" > .env.local
npm run dev
```

http://localhost:3000/login 접속.

## 서버 사전 작업

서버 환경변수에 admin 시드 + JWT secret 설정:

```bash
export ADMIN_BOOTSTRAP_LOGIN=admin
export ADMIN_BOOTSTRAP_PASSWORD=changeme
export ADMIN_JWT_SECRET=$(openssl rand -hex 32)
```

서버 부팅 시 admin_users 테이블에 idempotent insert.

## 현재 (Iter 4b)

- ✅ 로그인
- ✅ 대시보드 카드 (DAU/플레이/세션/총유저, `/v1/admin/dashboard`)
- ✅ 유저 검색 + 상세 + 코인 +/- (`/v1/admin/users/:id/coins`)
- ✅ 게임 카탈로그 리스트 + 편집 (`/v1/admin/games`, PUT `/v1/admin/games/:id`)
- ✅ 게임별 랭킹 top 100 (`/v1/admin/rankings/:gameId`)
- ✅ 최근 세션 50 (`/v1/admin/sessions`)
- ⏳ 공지/밴/푸시 + 인프라 배포 → Iter 4c

## Iter 4a 한계

- 토큰 저장은 localStorage (httpOnly cookie 전환은 후속)
- 비밀번호 해시는 sha256+salt (bcrypt 의존성 회피, 후속 전환)
- CORS 는 dev 와일드카드 (운영은 origin 화이트리스트로 좁힐 것)
- 페이지: 로그인 + 유저 검색 1개만
