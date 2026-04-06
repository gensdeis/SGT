# ArgoCD

GitOps 컨트롤 플레인. App-of-Apps 패턴으로 `infra/apps/` 아래의 모든 워크로드를 동기화한다.

## 설치 (스캐폴드 → 실 설치 시)

```bash
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

설치 후 `install.yaml` placeholder를 vendor한 매니페스트로 교체.

## 초기 비밀번호

```bash
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
```

## UI 접근

```bash
kubectl -n argocd port-forward svc/argocd-server 8080:443
# https://localhost:8080
```
