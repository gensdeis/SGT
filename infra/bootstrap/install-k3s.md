# k3s 로컬 단일 노드 설치 가이드

> 개발용. QA/Real 환경에는 별도 오버레이로 분리한다.

## 옵션 A — Linux/WSL2

```bash
curl -sfL https://get.k3s.io | sh -s - \
  --disable traefik \
  --write-kubeconfig-mode 644

# kubeconfig 복사
mkdir -p ~/.kube
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $USER ~/.kube/config
```

> Traefik은 ArgoCD가 `apps/traefik/`에서 직접 관리하므로 k3s 내장 버전은 비활성화한다.

## 옵션 B — Windows (Rancher Desktop)

1. Rancher Desktop 설치 (k3s 백엔드 선택)
2. Settings → Kubernetes → Traefik 비활성화
3. `kubectl get nodes`로 단일 노드 확인

## 검증

```bash
kubectl get nodes
kubectl get ns
```

## 다음 단계

```bash
kubectl apply -k ../clusters/local
```
