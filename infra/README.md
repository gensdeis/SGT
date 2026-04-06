# SGT Infra

숏게타(ShortGameTown) 개발/서비스 인프라.
계획서 v1.3 §11 스택: **k3s + ArgoCD + GitHub Actions + Traefik + Sealed Secrets**

> 현재 상태: **로컬 단일 노드 (개발용) — Docker Desktop Kubernetes**
> 매니페스트는 Docker Desktop 내장 k8s에서 바로 적용 가능한 상태.
> QA/Real 환경 구축 시 실제 k3s로 전환 (별도 클러스터 오버레이 추가).

---

## 디렉토리 구조

```
infra/
├── README.md                  # 이 파일
├── bootstrap/                 # 부트스트랩 가이드 (수동 1회 실행)
│   ├── docker-desktop.md      # 로컬: Docker Desktop Kubernetes (현재 사용)
│   └── install-k3s.md         # 참조: k3s 설치 가이드 (QA/Real 용)
├── clusters/
│   └── local/                 # 로컬 단일 노드 클러스터 오버레이
│       ├── kustomization.yaml
│       └── apps.yaml          # ArgoCD App-of-Apps 진입점
├── apps/                      # ArgoCD가 추적할 애플리케이션 매니페스트
│   ├── argocd/
│   ├── traefik/
│   ├── sealed-secrets/
│   └── shortgeta-server/      # Go 게임 서버 배포 (placeholder)
├── namespaces/                # 네임스페이스 정의
└── workflows/                 # GitHub Actions 워크플로우 (참조본)
    └── infra-validate.yaml
```

---

## 부트스트랩 절차 (로컬 단일 노드)

상세 가이드: [`bootstrap/docker-desktop.md`](bootstrap/docker-desktop.md)

1. Docker Desktop → Settings → Kubernetes 활성화
2. `kubectl apply -n argocd -f apps/argocd/install.yaml` (ArgoCD 설치)
3. `kubectl apply -k clusters/local` (App-of-Apps 적용)
4. `kubectl -n argocd port-forward svc/argocd-server 8080:443`로 UI 확인

> 로컬 단일 노드는 **개발 검증용**이다. QA/Real 환경 구축 시 별도 오버레이 추가 (`clusters/qa/`, `clusters/real/`).

## 핀된 버전

| 컴포넌트 | 버전 |
|----------|------|
| ArgoCD | v3.3.6 |
| Traefik (Helm chart) | 39.0.7 |
| Sealed Secrets (Helm chart) | 2.18.4 |

---

## 네임스페이스 정책

| 네임스페이스 | 용도 |
|--------------|------|
| `argocd` | ArgoCD 컨트롤 플레인 |
| `traefik` | Traefik 인그레스 |
| `sealed-secrets` | Sealed Secrets 컨트롤러 |
| `shortgeta-dev` | Go 게임 서버 (dev) |
| `shortgeta-ops` | 운영툴 (Phase 2~) |

---

## 관련 문서

- `Docs/INFRA_PLAN.md` — 인프라 계획 상세
- `Docs/GIT_STRATEGY.md` — 모노레포 구성
- `Docs/BACKEND_PLAN.md` — Go 백엔드 배포 대상
- `Docs/shortgeta-plan-v1.3.html` — 마스터 계획서
