# kind 부트스트랩 가이드 (로컬 단일 노드)

> **현재 사용 중인 로컬 환경.**
> Docker Desktop 내장 Kubernetes 가 WSL2 + cgroup v2 의 cpuset 컨트롤러
> 누락 이슈로 시작되지 않아, kind (Kubernetes IN Docker) 로 전환했다.

## 사전 조건

- Docker Desktop 설치 및 실행 중 (k8s 내장 기능은 **Disabled** 상태로 둘 것)
- `kind` 0.31.0+ 설치: `winget install Kubernetes.kind`

## 1. 클러스터 생성

```bash
kind create cluster --name shortgeta
```

생성 후 자동으로 `kubectl` 컨텍스트가 `kind-shortgeta` 로 설정된다.

```bash
kubectl config use-context kind-shortgeta
kubectl get nodes
# NAME                      STATUS   ROLES           AGE   VERSION
# shortgeta-control-plane   Ready    control-plane   ...   v1.35.0
```

## 2. ArgoCD 설치 (vendor 매니페스트)

```bash
kubectl create namespace argocd
kubectl apply --server-side --force-conflicts -n argocd -f infra/apps/argocd/install.yaml
kubectl -n argocd wait --for=condition=Available deploy/argocd-server --timeout=300s
kubectl -n argocd wait --for=condition=Ready pod -l app.kubernetes.io/name=argocd-repo-server --timeout=180s
```

> ⚠️ **반드시 `--server-side` 를 사용**한다. ArgoCD 의 ApplicationSets CRD 가
> client-side apply 의 annotation 256KB 한도를 초과해서 일반 `kubectl apply` 가 거부된다.

## 3. App-of-Apps 적용

```bash
kubectl apply -k infra/clusters/local
```

이 명령으로 다음이 적용된다:
- 5 개 네임스페이스 (`argocd`, `traefik`, `sealed-secrets`, `shortgeta-dev`, `shortgeta-ops`)
- `shortgeta-root` ArgoCD Application (App-of-Apps)

ArgoCD 가 자동으로 `infra/apps/*/application.yaml` 을 픽업해 다음 자식 Application 을 sync 한다:
- `traefik` (Helm chart 39.0.7, multi-source)
- `sealed-secrets` (Helm chart 2.18.4)

> ArgoCD 는 GitHub 의 `dev` 브랜치를 추적한다. 변경사항이 sync 되려면 `dev` 에 푸시되어 있어야 한다.

## 4. 동기화 확인

```bash
# 자식 Application 상태 (모두 Synced/Healthy 가 목표)
kubectl -n argocd get applications.argoproj.io

# 강제 새로고침이 필요하면:
kubectl -n argocd annotate application shortgeta-root \
  argocd.argoproj.io/refresh=hard --overwrite
```

## 5. ArgoCD UI 접근

```bash
# 초기 admin 비밀번호
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath="{.data.password}" | base64 -d

# 포트포워딩
kubectl -n argocd port-forward svc/argocd-server 8080:443
# https://localhost:8080  (admin / 위 비밀번호)
```

## 6. Sealed Secrets master key 백업 (필수)

```bash
kubectl -n sealed-secrets get secret \
  -l sealedsecrets.bitnami.com/sealed-secrets-key \
  -o yaml > sealed-secrets-master.key

# git 절대 커밋 금지. 1Password / 외부 안전 저장소에 보관.
```

## kind 특이사항

| 항목 | 동작 |
|------|------|
| LoadBalancer 서비스 | 기본적으로 `Pending` 상태 (External IP 없음). `cloud-provider-kind` 를 별도 실행하거나 `kubectl port-forward` / NodePort 사용 |
| PersistentVolume | hostPath 기반, kind 노드 컨테이너 안에 저장. `kind delete cluster` 시 사라짐 |
| 이미지 로딩 | 로컬 빌드 이미지는 `kind load docker-image <image>:<tag> --name shortgeta` 로 명시적 로드 필요 |
| Ingress 노출 | Traefik 을 LoadBalancer 로 띄워도 외부 접근 안 됨. `kubectl port-forward -n traefik svc/traefik 8443:443` 로 접근 |

## 클러스터 정리

```bash
kind delete cluster --name shortgeta
```

## 트러블슈팅

| 증상 | 해결 |
|------|------|
| `kind create cluster` 가 멈춤 | Docker Desktop 재시작, `kind delete cluster --name shortgeta` 후 재시도 |
| ArgoCD apply 시 annotation 한도 에러 | `--server-side --force-conflicts` 플래그 사용 |
| Application 이 `Unknown` 상태 | `kubectl -n argocd get pods` 로 repo-server Ready 여부 확인. 그래도 안 되면 hard refresh |
| Helm chart schema 에러 | 차트 버전과 values.yaml 호환성 확인 (예: Traefik v39 는 `ports.websecure.tls` 블록 비허용) |
