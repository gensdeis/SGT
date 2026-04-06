# Git 브랜치 전략

## 저장소 구성 (모노레포)

SGT git 저장소는 다음 4개 프로젝트를 모노레포로 구분 관리한다.
계획서 v1.3의 기술 스택(Unity 2022.3 LTS / Go / k3s + ArgoCD)에 맞춘다.

| # | 프로젝트 | 디렉토리(예정) | 스택 |
|---|----------|----------------|------|
| 1 | Unity 클라이언트 | `client/` | Unity 2022.3 LTS, Addressables, Claude MCP |
| 2 | Go 게임 서버 | `server/` | Go (Game Registry / Ranking / Analytics / Creator API) |
| 3 | 게임 운영툴 | `ops-tool/` | 운영/심사/콘텐츠 관리 (Phase 2~3 UGC 심사 포함) |
| 4 | 개발/서비스 인프라 | `infra/` | k3s, ArgoCD, GitHub Actions, Traefik, Sealed Secrets |

> CI/CD는 변경된 디렉토리에 따라 파이프라인을 분기한다 (path filter).

---

## 브랜치

| 브랜치 | 용도 | 푸시 정책 |
|--------|------|-----------|
| `main` | 백업 통합 | 권한 없이 직접 푸시 가능 |
| `dev` | 개발 | PR 필요 |
| `qa` | 라이브 서비스 전 검증 | PR 필요 |
| `real` | 현재 라이브(real) | PR 필요 |
| `hotfix` | 긴급 핫픽스용 | PR 필요 |
| `pre` | 이전 버전 롤백용 | PR 필요 |

---

## 규칙

- `main`에는 직접 푸시가 허용되지만, 그 외 모든 브랜치(`dev`, `qa`, `real`, `hotfix`, `pre`)는 반드시 Pull Request를 통해서만 병합한다.
