# shortgeta-server (placeholder, 비활성화)

Go 게임 서버 ArgoCD Application **placeholder**.

`application.yaml.disabled` 로 보관 — App-of-Apps 의 `include: '*/application.yaml'`
글롭에 매칭되지 않아 sync 되지 않는다.

## 활성화 시점

`server/deploy/overlays/dev` 가 실제로 생성되면 다음 명령으로 활성화한다:

```bash
mv infra/apps/shortgeta-server/application.yaml.disabled \
   infra/apps/shortgeta-server/application.yaml
git add -A && git commit -m "feat(infra): enable shortgeta-server ArgoCD app"
```

활성화 전까지는 `path: server/deploy/overlays/dev` 가 존재하지 않아 sync 가 실패한다.
