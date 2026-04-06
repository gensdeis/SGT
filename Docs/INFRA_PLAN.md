# 숏게타 — 인프라 계획

> 계획서 v1.3 §11 기준. 모노레포 `infra/` 디렉토리 구현 가이드.

---

## 스택 (v1.3 확정)

| 레이어 | 선택 |
|--------|------|
| 컨테이너 오케스트레이션 | k3s |
| GitOps | ArgoCD (App-of-Apps 패턴) |
| 인그레스 | Traefik (k3s 내장 비활성화 후 직접 관리) |
| 시크릿 | Sealed Secrets (Bitnami) |
| CI/CD | GitHub Actions |
| 매니페스트 형식 | Kustomize + 일부 Helm chart (ArgoCD Application) |

---

## 환경 분리

| 환경 | 클러스터 | 오버레이 | git 브랜치 |
|------|----------|----------|-----------|
| local | Docker Desktop Kubernetes (개발 PC) | `infra/clusters/local` | `dev` |
| qa | k3s (Phase 1 후반) | `infra/clusters/qa` (예정) | `qa` |
| real | k3s 멀티 노드 (출시 시점) | `infra/clusters/real` (예정) | `real` |

> 현재는 **local만 스캐폴드**. qa/real은 Phase 1 후반에 추가.
> 로컬은 Docker Desktop 내장 k8s, QA/Real은 v1.3 명시대로 k3s.

## 핀된 버전

| 컴포넌트 | 버전 |
|----------|------|
| ArgoCD | v3.3.6 |
| Traefik (Helm chart) | 39.0.7 |
| Sealed Secrets (Helm chart) | 2.18.4 |

---

## 디렉토리 구조

```
infra/
├── README.md
├── bootstrap/             # k3s 설치 가이드 (수동 1회)
├── clusters/local/        # 로컬 클러스터 진입점 (kustomization + App-of-Apps)
├── apps/                  # ArgoCD가 추적할 워크로드
│   ├── argocd/            # 자기 자신 (vendor 예정)
│   ├── traefik/           # Helm chart Application
│   ├── sealed-secrets/    # Helm chart Application
│   └── shortgeta-server/  # Go 서버 (placeholder)
├── namespaces/
└── workflows/             # GitHub Actions 참조본
```

---

## 부트스트랩 순서

1. **k3s 설치** — `infra/bootstrap/install-k3s.md`
   - Traefik 내장 비활성화 (`--disable traefik`)
2. **ArgoCD 설치** — 공식 install.yaml 적용 후 `apps/argocd/install.yaml`로 vendor
3. **App-of-Apps 적용** — `kubectl apply -k infra/clusters/local`
4. **Sealed Secrets 키 백업** — 컨트롤러 master key 안전 보관
5. **DNS / 도메인** — 로컬은 `/etc/hosts` 또는 `*.localtest.me`로 시작

---

## 보안 원칙

- Secret 평문은 git 커밋 금지 → **반드시 SealedSecret**으로 변환
- Sealed Secrets controller master key는 별도 백업 (git 외부)
- ArgoCD admin 비밀번호는 부트스트랩 직후 SSO 또는 강한 PW로 교체
- Traefik 대시보드는 운영 환경에서 비활성화

---

## Phase별 인프라 작업

### Phase 1 (지금)
- [x] `infra/` 스캐폴드 작성
- [x] GitHub repo URL 적용 (`gensdeis/SGT`)
- [x] ArgoCD v3.3.6 install.yaml vendor
- [x] Traefik 39.0.7 / Sealed Secrets 2.18.4 버전 핀
- [ ] Docker Desktop Kubernetes 활성화 + 부트스트랩 적용
- [ ] `.github/workflows/`로 검증 워크플로우 복사
- [ ] Go 서버 컨테이너 이미지 빌드 파이프라인
- [ ] `shortgeta-dev` 네임스페이스에 첫 배포

### Phase 2
- [ ] qa 클러스터 오버레이
- [ ] 운영툴 네임스페이스 추가
- [ ] 모니터링 스택 (Prometheus + Grafana 검토)
- [ ] 로그 수집 (Loki 검토)

### Phase 3
- [ ] real 클러스터 (멀티 노드) 마이그레이션
- [ ] CDN 연동 (Addressable 번들 + UGC)
- [ ] A/B 테스트 인프라 (BACKEND_PLAN.md 참조)

---

## 관련 문서

| 파일 | 용도 |
|------|------|
| `infra/README.md` | 디렉토리 구조 + 부트스트랩 요약 |
| `BACKEND_PLAN.md` | Go 서버 배포 대상 |
| `GIT_STRATEGY.md` | 모노레포 + 브랜치 정책 |
| `shortgeta-plan-v1.3.html` | 마스터 계획서 |
