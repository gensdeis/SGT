# Sealed Secrets

비밀값을 git에 암호화된 상태로 커밋. 컨트롤러만 평문 복호화 가능.

## kubeseal CLI 설치 후 사용 예

```bash
# 1) 일반 Secret 작성 (커밋 금지)
kubectl create secret generic api-key \
  --from-literal=key=supersecret \
  --dry-run=client -o yaml > /tmp/secret.yaml

# 2) SealedSecret으로 봉인 (커밋 가능)
kubeseal --controller-namespace sealed-secrets \
  --controller-name sealed-secrets-controller \
  -o yaml < /tmp/secret.yaml > sealed-api-key.yaml

# 3) git commit
```

## 컨트롤러 공개키 백업 (필수)

```bash
kubectl -n sealed-secrets get secret \
  -l sealedsecrets.bitnami.com/sealed-secrets-key \
  -o yaml > sealed-secrets-master.key
```

> `sealed-secrets-master.key`는 **git에 절대 커밋하지 말 것**.
