# shortgeta-server (활성)

Go 게임 서버 ArgoCD Application. dev 브랜치의 `server/deploy/overlays/dev` 를 추적한다.

## 활성화 상태

`application.yaml` (활성). App-of-Apps 의 include 글롭 `*/application.yaml` 에 매칭되어 ArgoCD 가 자동 sync 한다.

## 이미지 공급 방식 (Iteration 2~)

GHCR 자동 push 활성화. `main` 브랜치에 `server/` 변경이 푸시되면 GitHub Actions
`server-ci.yaml` 의 `docker-push` job 이 다음 두 태그를 push:

- `ghcr.io/gensdeis/shortgeta-server:<short_sha>` (불변, 롤백용 핀)
- `ghcr.io/gensdeis/shortgeta-server:main` (가변, deployment 가 추적)

`server/deploy/overlays/dev/deployment-patch.yaml` 의 `image: ghcr.io/gensdeis/shortgeta-server:main`
+ `imagePullPolicy: Always` 조합으로, 새 push 가 감지되면 다음 pod 재시작 시 자동으로
새 이미지를 pull. 매니페스트 변경 없이 이미지만 갱신할 때:

```bash
kubectl -n shortgeta-dev rollout restart deployment/shortgeta-server
```

GHCR 패키지는 **public** 으로 설정되어 있어 imagePullSecret 불필요.
qa/real 클러스터로 이전 시 private 전환 + ghcr-pull SealedSecret 추가 예정.

### 로컬 빌드 → kind 로드 (필요 시)

이미 GHCR 가 동작하므로 일반적으로 불필요. 단 CI 우회 디버깅 시:

```bash
cd server
docker build -t shortgeta-server:dev .
kind load docker-image shortgeta-server:dev --name shortgeta
# deployment 의 image 를 임시로 shortgeta-server:dev + IfNotPresent 로 patch
```

### 디버깅

```bash
# 강제 ArgoCD sync
kubectl -n argocd annotate application shortgeta-server-dev \
  argocd.argoproj.io/refresh=hard --overwrite

# Pod 상태 + 로그
kubectl -n shortgeta-dev get pods -w
kubectl -n shortgeta-dev logs deployment/shortgeta-server --tail=30

# port-forward 헬스체크
kubectl -n shortgeta-dev port-forward svc/shortgeta-server 18081:80
curl http://localhost:18081/health
```

## DB 마이그레이션

**Iteration 2 부터 자동 마이그레이션** — 서버 시작 시 `internal/migrate` (goose v3 라이브러리 모드) 가
`/app/db/migrations` 의 모든 Up SQL 을 자동 적용한다. `AUTO_MIGRATE=false` 환경변수로 비활성화 가능
(긴급 디버깅용).

수동으로 마이그레이션을 돌릴 필요가 없다. 새 마이그레이션 파일을 `server/db/migrations/`
에 추가하고 commit 하면 다음 pod 시작 시 자동 적용.

## 비활성화 (필요 시)

```bash
mv application.yaml application.yaml.disabled
git commit -am "chore(infra): disable shortgeta-server temporarily"
```
