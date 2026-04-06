# Docker Desktop Kubernetes 부트스트랩 가이드

> 로컬 단일 노드 개발 환경. Docker Desktop 내장 Kubernetes 사용.
> (계획서 v1.3은 k3s를 명시하지만, 로컬 개발은 Docker Desktop의 단일 노드 k8s로 대체.
>  QA/Real 환경 구축 시 k3s로 전환)

## 1. Docker Desktop Kubernetes 활성화

1. Docker Desktop 실행
2. Settings → Kubernetes → **Enable Kubernetes** 체크 → Apply & Restart
3. 우측 하단 초록 점이 들어오면 준비 완료

## 2. 컨텍스트 확인

```bash
kubectl config use-context docker-desktop
kubectl get nodes
# NAME             STATUS   ROLES           AGE   VERSION
# docker-desktop   Ready    control-plane   ...   v1.xx.x
```

## 3. ArgoCD 설치 (vendor된 매니페스트)

```bash
kubectl create namespace argocd
kubectl apply -n argocd -f infra/apps/argocd/install.yaml
kubectl -n argocd wait --for=condition=Available deploy/argocd-server --timeout=300s
```

## 4. App-of-Apps 적용

```bash
kubectl apply -k infra/clusters/local
```

ArgoCD가 `infra/apps/` 아래의 모든 Application을 자동으로 동기화한다.

## 5. ArgoCD UI 접근

```bash
# 초기 admin 비밀번호
kubectl -n argocd get secret argocd-initial-admin-secret \
  -o jsonpath="{.data.password}" | base64 -d

# 포트포워딩
kubectl -n argocd port-forward svc/argocd-server 8080:443
# https://localhost:8080  (admin / 위 비밀번호)
```

## 6. Sealed Secrets master key 백업

```bash
kubectl -n sealed-secrets get secret \
  -l sealedsecrets.bitnami.com/sealed-secrets-key \
  -o yaml > sealed-secrets-master.key

# 이 파일은 git에 절대 커밋 금지. 안전한 외부 저장소에 보관.
```

## Docker Desktop 특이사항

- **LoadBalancer**: Docker Desktop은 자체 vpnkit으로 `localhost`에 직접 노출
- **PersistentVolume**: hostPath 기반, Docker Desktop 종료 시 데이터 유지
- **이미지**: 로컬 빌드 이미지가 자동으로 클러스터에서 사용 가능 (`imagePullPolicy: IfNotPresent`)

## 트러블슈팅

| 증상 | 해결 |
|------|------|
| `localhost:80`이 응답 안 함 | Docker Desktop 재시작 또는 포트 충돌 확인 |
| ArgoCD sync 실패 (repo 인증) | private repo면 ArgoCD에 SSH key 등록 |
| Sealed Secrets controller 미기동 | `kubectl -n sealed-secrets get pods` 확인 |
