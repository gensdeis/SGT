# 미니게임 프로젝트 — 개발 인프라 계획서 v3

## 개요

서버/클라이언트 빌드부터 배포, 모니터링, 로그 수집까지
개발자가 코드 작성에만 집중할 수 있는 인프라 환경을 구축한다.

v2 대비 변경 사항:
- GHCR Private 이미지 인증 (imagePullSecrets) 추가
- Sealed Secrets 마스터 키 백업 절차 추가
- Traefik Ingress 설정 추가 (k3s 기본 내장, 별도 설치 불필요)

---

## 전체 파이프라인 흐름

```
[ 개발자 ]
    └─ git push
          ↓
[ GitHub Actions ]
    ├─ Go 서버: 테스트 → 빌드 → Docker 이미지 → GHCR push
    ├─ Unity 클라이언트: 빌드 → Steam 업로드 (main 브랜치만)
    └─ k8s 매니페스트 이미지 태그 자동 업데이트
          ↓
[ ArgoCD ]
    └─ 매니페스트 변경 감지 → k3s 클러스터 자동 배포
          ↓
[ k3s 클러스터 ]
    ├─ Traefik Ingress (기본 내장) → api.minigame.com 라우팅
    ├─ dev 네임스페이스  (개발 환경)
    └─ prod 네임스페이스 (서비스 환경)
          ↓
[ Grafana Stack ]
    ├─ Prometheus → 메트릭 수집
    └─ Promtail + Loki → 로그 수집
```

---

## 환경 구분

| 환경 | 용도 | 배포 조건 |
|---|---|---|
| dev | 기능 개발 및 테스트 | `develop` 브랜치 push 시 자동 배포 |
| prod | 실제 서비스 | `main` 브랜치 태그 (`v*`) 생성 시 배포 |

---

## GitHub Actions — CI/CD 파이프라인

### Go 서버 파이프라인

```yaml
# .github/workflows/server-ci.yml
name: Server CI/CD

on:
  push:
    branches: [main, develop]
    paths: ['server/**']

env:
  IMAGE_NAME: ghcr.io/${{ github.repository_owner }}/minigame-server

jobs:
  test-and-build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Go
        uses: actions/setup-go@v5
        with:
          go-version: '1.21'

      - name: Run tests
        run: go test ./...
        working-directory: server

      - name: Set image tag
        id: tag
        run: echo "tag=${GITHUB_SHA::8}" >> $GITHUB_OUTPUT

      - name: Log in to GHCR
        uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          context: server
          push: true
          tags: ${{ env.IMAGE_NAME }}:${{ steps.tag.outputs.tag }}

  update-manifest:
    needs: test-and-build
    runs-on: ubuntu-latest
    steps:
      - name: Checkout k8s-manifests
        uses: actions/checkout@v4
        with:
          repository: myorg/k8s-manifests
          token: ${{ secrets.MANIFEST_REPO_TOKEN }}

      - name: Determine overlay
        id: overlay
        run: |
          if [[ "${{ github.ref }}" == "refs/heads/main" ]]; then
            echo "path=overlays/prod" >> $GITHUB_OUTPUT
          else
            echo "path=overlays/dev" >> $GITHUB_OUTPUT
          fi

      - name: Update image tag
        run: |
          TAG=${GITHUB_SHA::8}
          sed -i "s|image: ghcr.io/myorg/minigame-server:.*|image: ghcr.io/myorg/minigame-server:${TAG}|" \
            ${{ steps.overlay.outputs.path }}/deployment-patch.yaml

      - name: Commit and push
        run: |
          git config user.name  "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git commit -am "chore: update server image to ${GITHUB_SHA::8}"
          git push
```

### Unity 클라이언트 파이프라인

```yaml
# .github/workflows/client-ci.yml
name: Client CI/CD

on:
  push:
    branches: [main, develop]
    paths: ['client/**']

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          lfs: true

      - name: Cache Library
        uses: actions/cache@v4
        with:
          path: client/Library
          key: Library-${{ hashFiles('client/Assets/**', 'client/Packages/**') }}

      - name: Build Unity project
        uses: game-ci/unity-builder@v4
        env:
          UNITY_LICENSE:  ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL:    ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          projectPath:    client
          targetPlatform: StandaloneWindows64
          buildName:      MinigameCollection

      - name: Upload build artifact
        uses: actions/upload-artifact@v4
        with:
          name: build-windows
          path: build/StandaloneWindows64

  steam-deploy:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Download build artifact
        uses: actions/download-artifact@v4
        with:
          name: build-windows
          path: build

      - name: Deploy to Steam
        uses: game-ci/steam-deploy@v3
        with:
          username:        ${{ secrets.STEAM_USERNAME }}
          configVdf:       ${{ secrets.STEAM_CONFIG_VDF }}
          appId:           ${{ secrets.STEAM_APP_ID }}
          buildDescription: ${{ github.sha }}
          rootPath:        build
          depot1Path:      StandaloneWindows64
```

---

## Docker — 서버 컨테이너화

```dockerfile
# server/Dockerfile

FROM golang:1.21-alpine AS builder
WORKDIR /app
COPY go.mod go.sum ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -o server ./cmd/server

FROM alpine:3.19
RUN apk add --no-cache ca-certificates tzdata
WORKDIR /app
COPY --from=builder /app/server .
COPY config/ config/
EXPOSE 8080
CMD ["./server"]
```

---

## k3s — 단일 노드 Kubernetes 클러스터

### 서버 선택 비교

| 옵션 | 사양 | 비용 | 비고 |
|---|---|---|---|
| AWS EC2 t3.medium | 2 vCPU / 4GB RAM | ~$30/월 | AWS 생태계 친숙 |
| Hetzner CX22 | 2 vCPU / 4GB RAM | ~$6/월 | 가성비 최강, 유럽 리전 |
| Vultr Regular | 2 vCPU / 4GB RAM | ~$12/월 | 아시아 리전 다수 |

### k3s 설치

```bash
# 마스터 노드 설치
curl -sfL https://get.k3s.io | sh -

# 로컬 PC 에서 kubectl 사용
scp user@server-ip:/etc/rancher/k3s/k3s.yaml ~/.kube/config
sed -i 's/127.0.0.1/서버IP/' ~/.kube/config

# 노드 추가 (스케일아웃 시)
curl -sfL https://get.k3s.io | K3S_URL=https://마스터IP:6443 K3S_TOKEN=노드토큰 sh -
```

---

## GHCR Private 이미지 인증

GHCR 이미지가 Private 일 경우, k3s 가 이미지를 pull 할 권한이 없어
`ImagePullBackOff` 에러가 발생한다. PAT 기반 Secret 을 생성해 Deployment 에 연결한다.

### 1단계 — GitHub PAT 발급

GitHub → Settings → Developer Settings → Personal Access Tokens → Fine-grained tokens
- 권한: `read:packages` 체크
- 만료 기간: 1년 (만료 시 갱신 후 Secret 업데이트 필요)

### 2단계 — k3s 클러스터에 docker-registry Secret 생성

```bash
# dev 네임스페이스
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  --namespace dev

# prod 네임스페이스 (동일하게 생성)
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  --namespace prod
```

### 3단계 — Deployment 에 imagePullSecrets 추가

```yaml
# base/deployment.yaml
spec:
  replicas: 1
  selector:
    matchLabels:
      app: minigame-server
  template:
    spec:
      imagePullSecrets:          # ← 추가
        - name: ghcr-secret      # ← 위에서 생성한 Secret 이름
      containers:
        - name: server
          image: ghcr.io/myorg/minigame-server:latest
          ports:
            - containerPort: 8080
          envFrom:
            - secretRef:
                name: minigame-secrets
          resources:
            requests:
              cpu: "100m"
              memory: "128Mi"
            limits:
              cpu: "500m"
              memory: "256Mi"
          livenessProbe:
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
```

> `ghcr-secret` 은 Sealed Secrets 로 암호화해 git 에 커밋한다.
> PAT 가 만료되면 Secret 을 재생성하고 Sealed Secret 도 재암호화해야 한다.

---

## Sealed Secrets — 마스터 키 백업

Sealed Secrets 는 클러스터 고유의 비대칭 키로 암호화/복호화한다.
클러스터를 재생성하면 기존 마스터 키가 사라져 git 에 저장된 sealed-secret 파일을 영영 복호화할 수 없게 된다.

**Controller 설치 직후 반드시 마스터 키를 백업해야 한다.**

### 설치

```bash
kubectl apply -f https://github.com/bitnami-labs/sealed-secrets/releases/latest/download/controller.yaml
```

### 마스터 키 백업 (설치 직후 필수)

```bash
# 마스터 키 추출
kubectl get secret \
  -n kube-system \
  -l sealedsecrets.bitnami.com/sealed-secrets-key \
  -o yaml > master-key-backup.yaml

# 백업 파일 확인
cat master-key-backup.yaml
```

> ⚠️ `master-key-backup.yaml` 은 절대 git 에 커밋하지 않는다.
> 아래 중 하나의 안전한 저장소에 보관한다.

| 저장소 | 방법 |
|---|---|
| AWS Secrets Manager | `aws secretsmanager create-secret --name sealed-secrets-master-key --secret-string file://master-key-backup.yaml` |
| 1Password | Secure Note 로 저장 |
| 로컬 안전 저장소 | 암호화된 디스크 또는 USB 에 보관 |

### 클러스터 재생성 시 마스터 키 복구

```bash
# 새 클러스터에 기존 마스터 키 복원
kubectl apply -f master-key-backup.yaml

# Controller 재시작 (키 인식)
kubectl rollout restart deployment sealed-secrets-controller -n kube-system
```

### Sealed Secret 생성 및 사용

```bash
# 평문 Secret → Sealed Secret 변환 (git 커밋 가능)
kubeseal --format yaml < secret.yaml > sealed-secret.yaml

# 적용
kubectl apply -f sealed-secret.yaml
```

---

## Traefik Ingress — 외부 트래픽 라우팅

k3s 는 Traefik Ingress Controller 를 기본으로 내장하고 있어 별도 설치가 필요 없다.
Ingress 리소스만 추가하면 `api.minigame.com` 을 Go 서버 서비스로 라우팅할 수 있다.

### 네임스페이스 구조 업데이트

```
k8s-manifests/
├── base/
│   ├── deployment.yaml     # imagePullSecrets 포함
│   ├── service.yaml
│   ├── ingress.yaml        # ← 추가
│   └── configmap.yaml
└── overlays/
    ├── dev/
    │   ├── kustomization.yaml
    │   └── deployment-patch.yaml   # replica: 1
    └── prod/
        ├── kustomization.yaml
        └── deployment-patch.yaml   # replica: 2
```

### base/service.yaml

```yaml
apiVersion: v1
kind: Service
metadata:
  name: minigame-server
spec:
  selector:
    app: minigame-server
  ports:
    - protocol: TCP
      port: 80
      targetPort: 8080
```

### base/ingress.yaml

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: minigame-server
  annotations:
    # Traefik 기본 내장 — 별도 IngressClass 설치 불필요
    traefik.ingress.kubernetes.io/router.entrypoints: websecure
    # Let's Encrypt 자동 TLS (cert-manager 설치 시)
    cert-manager.io/cluster-issuer: letsencrypt-prod
spec:
  ingressClassName: traefik
  tls:
    - hosts:
        - api.minigame.com
      secretName: minigame-tls
  rules:
    - host: api.minigame.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: minigame-server
                port:
                  number: 80
```

### TLS 인증서 자동 발급 (cert-manager)

Traefik 과 cert-manager 를 연동하면 Let's Encrypt TLS 인증서를 자동으로 발급/갱신한다.

```bash
# cert-manager 설치
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/latest/download/cert-manager.yaml

# Let's Encrypt ClusterIssuer 등록
kubectl apply -f - <<EOF
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
      - http01:
          ingress:
            class: traefik
EOF
```

### dev 환경 Ingress (서브도메인 분리)

```yaml
# overlays/dev/ingress-patch.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: minigame-server
spec:
  rules:
    - host: dev.api.minigame.com   # dev 는 서브도메인으로 분리
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: minigame-server
                port:
                  number: 80
```

| 환경 | 도메인 |
|---|---|
| prod | `api.minigame.com` |
| dev | `dev.api.minigame.com` |

---

## ArgoCD — GitOps 배포

```bash
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

```yaml
# argocd/app-dev.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: minigame-server-dev
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/myorg/k8s-manifests
    targetRevision: HEAD
    path: overlays/dev
  destination:
    server: https://kubernetes.default.svc
    namespace: dev
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

```yaml
# argocd/app-prod.yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: minigame-server-prod
  namespace: argocd
spec:
  source:
    path: overlays/prod
  destination:
    namespace: prod
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

---

## Terraform — 인프라 코드화

```
terraform/
├── modules/
│   ├── k3s-node/       # EC2 + k3s 설치 자동화
│   ├── database/       # RDS PostgreSQL
│   ├── redis/          # ElastiCache
│   └── networking/     # VPC, 서브넷, 보안 그룹
└── environments/
    ├── dev/
    │   ├── main.tf
    │   └── terraform.tfvars
    └── prod/
        ├── main.tf
        └── terraform.tfvars
```

```hcl
# modules/k3s-node/main.tf
resource "aws_instance" "k3s_node" {
  ami           = var.ami_id
  instance_type = var.instance_type
  key_name      = var.key_name

  user_data = <<-EOF
    #!/bin/bash
    curl -sfL https://get.k3s.io | sh -
  EOF

  tags = {
    Name        = "k3s-${var.env}"
    Environment = var.env
  }
}
```

---

## Grafana Stack — 메트릭 + 로그 통합 관측성

```bash
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

helm install monitoring grafana/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace

helm install loki grafana/loki-stack \
  --namespace monitoring \
  --set promtail.enabled=true \
  --set loki.enabled=true
```

### Grafana 대시보드 주요 패널

| 패널 | 데이터 소스 | 쿼리 |
|---|---|---|
| API 응답 시간 (p50/p95/p99) | Prometheus | `http_request_duration_seconds` |
| 요청 수 / 에러율 | Prometheus | `http_requests_total` |
| 고루틴 수 | Prometheus | `go_goroutines` |
| 메모리 사용량 | Prometheus | `go_memstats_alloc_bytes` |
| DB 연결 사용률 | Prometheus | `db_pool_acquired_conns` |
| Worker 큐 깊이 | Prometheus | `analytics_queue_depth` |
| 실시간 로그 스트림 | Loki | `{namespace="prod", app="minigame-server"}` |
| 어뷰징 차단 로그 | Loki | `{app="minigame-server"} \|= "invalid_signature"` |
| 에러 로그 필터 | Loki | `{app="minigame-server"} \| json \| level="error"` |

### Grafana 알림 기준

| 조건 | 알림 채널 |
|---|---|
| p99 응답시간 > 2초 | Slack `#dev-alerts` |
| 고루틴 수 > 500 | Slack `#dev-alerts` |
| DB 연결 사용률 > 80% | Slack `#dev-alerts` |
| Worker 큐 깊이 > 800 | Slack `#dev-alerts` |
| 5xx 에러율 > 1% | Slack `#prod-alerts` (즉시 대응) |

---

## 레포지토리 구조

| 레포 | 역할 |
|---|---|
| `minigame-server` | Go 백엔드 소스 + Dockerfile + GitHub Actions |
| `minigame-client` | Unity 클라이언트 소스 + GitHub Actions |
| `k8s-manifests` | K8s 매니페스트 + Kustomize overlays (ArgoCD 가 바라보는 레포) |
| `terraform` | 인프라 코드 (EC2, DB, Redis 등) |

---

## 인프라 구축 순서

| 단계 | 작업 |
|---|---|
| 1 | Terraform 으로 EC2 프로비저닝 + k3s 자동 설치 |
| 2 | Sealed Secrets Controller 설치 → **마스터 키 즉시 백업** |
| 3 | GHCR PAT 발급 → dev/prod 네임스페이스에 `ghcr-secret` 생성 → Sealed Secret 변환 |
| 4 | cert-manager 설치 + Let's Encrypt ClusterIssuer 등록 |
| 5 | ArgoCD 설치 + k8s-manifests 레포 연결 (dev/prod 앱 등록) |
| 6 | GitHub Actions Go 서버 파이프라인 구성 + GHCR push 확인 |
| 7 | 매니페스트 자동 업데이트 → ArgoCD dev 배포 + Ingress 라우팅 확인 |
| 8 | Helm 으로 Grafana + Loki + Prometheus 설치 + 대시보드 구성 |
| 9 | GitHub Actions Unity 클라이언트 파이프라인 구성 |
| 10 | prod 환경 배포 + Steam 업로드 테스트 |
| 11 | 전체 파이프라인 end-to-end 검증 |

---

## 비용 예상

| 항목 | 비용 |
|---|---|
| k3s Control Plane | $0 |
| GitHub Actions | $0 (public 레포 기준) |
| EC2 t3.medium 또는 Vultr | $12~30/월 |
| DB (Supabase 무료) | $0 |
| Redis (Upstash 무료) | $0 |
| TLS 인증서 (Let's Encrypt) | $0 |
| **합계** | **$12~30/월** |

> dev/prod 를 동일 k3s 노드의 네임스페이스로 분리하면 단일 서버 비용만 발생한다.
> 트래픽이 늘어 노드를 분리해야 할 시점에 노드를 추가(join) 하면 된다.
