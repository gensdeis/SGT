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

## Sealed Secrets — 마스터 키 백업 (필수 절차)

Sealed Secrets 컨트롤러는 클러스터 고유의 비대칭 키로 암호화/복호화한다.
**클러스터를 재생성하면 마스터 키가 사라져, git 에 저장된 sealed-secret 파일을 영영 복호화할 수 없게 된다.**

> ⚠️ 컨트롤러 설치 직후 **반드시** 마스터 키를 백업한다.

### 백업

```bash
kubectl -n sealed-secrets get secret \
  -l sealedsecrets.bitnami.com/sealed-secrets-key \
  -o yaml > sealed-secrets-master.key

cat sealed-secrets-master.key  # 정상 추출 확인
```

> ⚠️ `sealed-secrets-master.key` 는 **절대 git 에 커밋 금지.** `.gitignore` 에 `**/sealed-secrets-master.key` 추가 필수.

### 권장 보관소

| 저장소 | 방법 |
|--------|------|
| 1Password | Secure Note |
| AWS Secrets Manager | `aws secretsmanager create-secret --name sealed-secrets-master-key --secret-string file://sealed-secrets-master.key` |
| 로컬 암호화 디스크 / USB | 오프라인 보관 |

### 클러스터 재생성 시 복구

```bash
kubectl apply -f sealed-secrets-master.key
kubectl rollout restart deployment sealed-secrets-controller -n sealed-secrets
```

### Sealed Secret 생성 절차

```bash
# 1) 평문 Secret 작성
cat > /tmp/secret.yaml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: shortgeta-secrets
  namespace: shortgeta-dev
type: Opaque
stringData:
  DATABASE_URL: "postgres://..."
  HMAC_BASE_KEY: "..."
  BUILD_GUID: "..."
EOF

# 2) Sealed Secret 으로 봉인 (커밋 가능)
kubeseal --format yaml \
  --controller-namespace sealed-secrets \
  --controller-name sealed-secrets-controller \
  < /tmp/secret.yaml > sealed-shortgeta-secrets.yaml

# 3) 평문 즉시 삭제
rm /tmp/secret.yaml

# 4) git commit (암호화본만)
git add sealed-shortgeta-secrets.yaml
git commit -m "chore: update shortgeta-dev secrets"
```

---

## GHCR Private 이미지 인증 (imagePullSecrets)

GitHub Container Registry 가 Private 이면 클러스터가 이미지를 pull 할 권한이 없어 `ImagePullBackOff` 가 발생한다.

### 1단계 — GitHub PAT 발급

GitHub → Settings → Developer Settings → Personal Access Tokens → **Fine-grained tokens**
- 권한: `read:packages`
- 만료: 1년 (만료 시 갱신 필요)

### 2단계 — 네임스페이스별 docker-registry Secret 생성

```bash
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=gensdeis \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  --namespace shortgeta-dev
```

> 이 Secret 은 Sealed Secret 으로 변환해 git 에 커밋하는 것을 권장.

### 3단계 — Deployment 에 imagePullSecrets 추가

```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: ghcr-secret
      containers:
        - name: server
          image: ghcr.io/gensdeis/shortgeta-server:latest
```

> PAT 만료 시 Secret 재생성 + Sealed Secret 재암호화.

---

## cert-manager + Let's Encrypt (TLS 자동 발급)

QA/Real 환경에서 Traefik 과 cert-manager 를 연동해 Let's Encrypt 인증서를 자동 발급/갱신한다.

```bash
# 설치 (qa/real 클러스터에서)
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

# Let's Encrypt ClusterIssuer 등록
kubectl apply -f - <<'EOF'
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: ops@shortgeta.example
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
      - http01:
          ingress:
            class: traefik
EOF
```

### Ingress 예시

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: shortgeta-server
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    traefik.ingress.kubernetes.io/router.entrypoints: websecure
spec:
  ingressClassName: traefik
  tls:
    - hosts: [api.shortgeta.example]
      secretName: shortgeta-tls
  rules:
    - host: api.shortgeta.example
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: shortgeta-server
                port:
                  number: 80
```

| 환경 | 도메인 (예정) |
|------|---------------|
| local | `localtest.me` 또는 `/etc/hosts` |
| qa | `qa.api.shortgeta.example` |
| real | `api.shortgeta.example` |

> 로컬은 인증서 불필요 (Docker Desktop 내부 라우팅).

---

## Grafana Stack — 관측성 (메트릭 + 로그)

Phase 2 에서 도입. Helm 으로 일괄 설치한다.

```bash
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

helm install monitoring grafana/kube-prometheus-stack \
  --namespace monitoring --create-namespace

helm install loki grafana/loki-stack \
  --namespace monitoring \
  --set promtail.enabled=true \
  --set loki.enabled=true
```

### 주요 대시보드 패널

| 패널 | 데이터 소스 | 쿼리 |
|------|-------------|------|
| API 응답 시간 (p50/p95/p99) | Prometheus | `http_request_duration_seconds` |
| 요청 수 / 에러율 | Prometheus | `http_requests_total` |
| 고루틴 수 | Prometheus | `go_goroutines` |
| 메모리 사용량 | Prometheus | `go_memstats_alloc_bytes` |
| DB 연결 사용률 | Prometheus | `db_pool_acquired_conns` |
| Analytics/Ranking 큐 깊이 | Prometheus | `analytics_queue_depth`, `ranking_queue_depth` |
| 실시간 로그 | Loki | `{namespace="shortgeta-real", app="shortgeta-server"}` |
| 어뷰징 로그 | Loki | `{app="shortgeta-server"} \|= "invalid_signature"` |
| 에러 로그 필터 | Loki | `{app="shortgeta-server"} \| json \| level="error"` |

### 알림 채널

| 조건 | 채널 |
|------|------|
| p99 응답시간 > 2초 | Slack `#dev-alerts` |
| 고루틴 수 > 500 | Slack `#dev-alerts` |
| DB 연결 사용률 > 80% | Slack `#dev-alerts` |
| Worker 큐 깊이 > 800 | Slack `#dev-alerts` |
| 5xx 에러율 > 1% | Slack `#prod-alerts` (즉시 대응) |

> 메트릭/로그 명세는 `BACKEND_PLAN.md` 의 관측성 절과 일치한다.

---

## 장애 대응 절차

### 등급

| 등급 | 기준 | 목표 복구 시간 |
|------|------|----------------|
| P1 | real 서버 다운 / 점수 제출 전체 불가 | 30분 |
| P2 | 랭킹 갱신 지연 / 일부 API 오류 | 2시간 |
| P3 | 로그/모니터링 이상 | 다음 근무일 |

### P1 대응 순서

```bash
# 1. 현재 상태 파악
kubectl -n shortgeta-real get pods
kubectl -n shortgeta-real describe pod <문제 파드>
kubectl -n shortgeta-real logs <문제 파드> --previous

# 2. Grafana / Loki 에서 에러 시점 특정
# Loki: {namespace="shortgeta-real"} |= "error" | json

# 3a. 코드 버그 → ArgoCD 로 이전 이미지 롤백
argocd app rollback shortgeta-server-real

# 3b. 인프라 문제 → Pod 재시작
kubectl -n shortgeta-real rollout restart deployment/shortgeta-server

# 3c. DB 연결 고갈 → Pool 메트릭 확인 + Pod 재시작
# Grafana: db_pool_acquired_conns / MaxConns

# 4. 복구 확인
kubectl -n shortgeta-real rollout status deployment/shortgeta-server
curl https://api.shortgeta.example/health
```

### 자주 발생하는 문제

**ImagePullBackOff** — GHCR PAT 만료
```bash
kubectl -n shortgeta-real delete secret ghcr-secret
kubectl -n shortgeta-real create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io --docker-username=gensdeis \
  --docker-password=NEW_PAT --docker-email=YOUR_EMAIL
```

**ArgoCD 동기화 실패**
```bash
argocd app get shortgeta-server-real
argocd app sync shortgeta-server-real --force
kubectl apply --dry-run=client -k server/deploy/overlays/real
```

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
- [ ] qa 클러스터 오버레이 (k3s)
- [ ] 운영툴 네임스페이스 추가
- [ ] cert-manager + Let's Encrypt ClusterIssuer
- [ ] kube-prometheus-stack 설치 (Prometheus + Grafana)
- [ ] loki-stack 설치 (Loki + Promtail)
- [ ] Grafana 알림 채널 (Slack #dev-alerts / #prod-alerts) 연결
- [ ] 장애 대응 P1/P2/P3 런북 검증

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
