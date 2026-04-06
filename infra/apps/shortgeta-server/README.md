# shortgeta-server (활성)

Go 게임 서버 ArgoCD Application. dev 브랜치의 `server/deploy/overlays/dev` 를 추적한다.

## 활성화 상태

`application.yaml` (활성). App-of-Apps 의 include 글롭 `*/application.yaml` 에 매칭되어 ArgoCD 가 자동 sync 한다.

## 이미지 공급 방식 (Phase 1, dev 클러스터)

GHCR 푸시는 Iteration 2 에서 셋업 예정. 현재는 사용자가 로컬에서 빌드해 kind 클러스터에 직접 로드한다:

```bash
# 1) 이미지 빌드
cd server
docker build -t shortgeta-server:dev .

# 2) kind 클러스터로 이미지 로드 (아무 노드에서나 사용 가능)
kind load docker-image shortgeta-server:dev --name shortgeta

# 3) ArgoCD 가 자동으로 sync. 강제로 새로고침하려면:
kubectl -n argocd annotate application shortgeta-server-dev \
  argocd.argoproj.io/refresh=hard --overwrite

# 4) Pod 상태 확인
kubectl -n shortgeta-dev get pods -w

# 5) port-forward 로 헬스체크
kubectl -n shortgeta-dev port-forward svc/shortgeta-server 18081:80
curl http://localhost:18081/health
```

## DB 마이그레이션 (최초 1회)

ArgoCD 가 PostgreSQL StatefulSet 을 띄우지만 스키마는 자동 생성하지 않는다.
첫 배포 후 한 번만 수동으로 적용한다 (goose annotation 제외 Up SQL 만 추출):

```bash
# 1) goose annotation 을 제거한 Up SQL 만 추출
python -c "
import re
src = open('server/db/migrations/20260406120001_init.sql', encoding='utf-8').read()
m = re.search(r'-- \+goose Up\s*\n-- \+goose StatementBegin\s*(.*?)-- \+goose StatementEnd', src, re.S)
print(m.group(1).strip())
" > /tmp/init_up.sql

# 2) 인-클러스터 postgres 에 적용
kubectl -n shortgeta-dev exec -i statefulset/shortgeta-postgres \
  -- psql -U shortgeta -d shortgeta_dev < /tmp/init_up.sql
```

> Iteration 2: 서버 시작 시 goose 라이브러리로 자동 마이그레이션 옵션 추가 예정.

## 비활성화 (필요 시)

```bash
mv application.yaml application.yaml.disabled
git commit -am "chore(infra): disable shortgeta-server temporarily"
```

> Iteration 2 이후 GHCR push 가 셋업되면 위 수동 빌드 단계는 제거된다.
