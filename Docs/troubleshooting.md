# 숏게타 트러블슈팅 로그

개발 중 발생한 에러와 해결 방법을 누적 기록한다.
새 에러를 만나면 이 문서 맨 아래에 항목을 추가한다.

> 형식: `## YYYY-MM-DD — 짧은 제목`
> 각 항목은 **증상 / 원인 / 해결 / 재발 방지** 4단 구성.

---

## 2026-04-06 — Docker Desktop k8s 의 cpuset cgroup 에러

**증상**
```
Reset Cluster 시도 → Unable to start a cluster
cgroup ["kubepods"] missing controllers: cpuset
```

**원인**
Docker Desktop 의 내장 Kubernetes 가 WSL2 cgroup v2 환경에서 cpuset 컨트롤러를 못 찾음.

**해결**
Docker Desktop k8s 를 끄고 **kind (Kubernetes IN Docker)** 로 전환.

**재발 방지**
- 인프라 문서 (`Docs/INFRA_PLAN.md`) 에 kind 사용 명시
- Docker Desktop k8s 는 시도하지 않는다

---

## 2026-04-06 — kustomize load restriction 에러

**증상**
ArgoCD Application 이 `../../namespaces/namespaces.yaml` 참조 시 "load restrictions" 거부.

**원인**
Kustomize 는 base 디렉토리 밖 파일을 직접 참조하면 보안상 거부.

**해결**
`infra/namespaces/` 를 자체 `kustomization.yaml` 을 가진 base 로 만들어서 resources 로 참조.

**재발 방지**
- 외부 파일은 반드시 그 디렉토리에 `kustomization.yaml` 있어야 base 로 참조 가능

---

## 2026-04-06 — App-of-Apps 가 helm values + install bundle 까지 apply 시도

**증상**
ArgoCD root app 이 traefik/values.yaml + argocd/install.yaml 까지 apply 시도해 sync 실패.

**원인**
`directory.recurse: true` 로 모든 yaml 을 manifest 로 인식.

**해결**
```yaml
directory:
  recurse: true
  include: '{*/application.yaml}'
```
App Application 매니페스트만 글롭으로 픽업.

**재발 방지**
- 새 ArgoCD app 추가 시 파일명을 `application.yaml` 로 통일

---

## 2026-04-06 — ApplicationSets CRD 너무 길어 client-side apply 실패

**증상**
`kubectl apply -f argocd-install.yaml` → annotation 256KB 한계 초과.

**해결**
```bash
kubectl apply --server-side --force-conflicts -f argocd-install.yaml
```

---

## 2026-04-06 — Traefik chart v39 schema 거부

**증상**
`ports.websecure.tls.enabled` 설정이 schema validation 실패.

**원인**
Traefik chart v28+ 부터 TLS 가 자동 활성화됨, 명시 설정 키 deprecated.

**해결**
values.yaml 에서 해당 블록 제거.

---

## 2026-04-06 — TMP Importer 가 Play 모드에서 import 실패

**증상**
Bootstrap.unity 열고 ▶ Play 누르면 TMP Importer 창 → "Failed to import package".

**원인**
Play 모드 중에는 패키지 import 가 막힘.

**해결**
1. Play 모드 종료
2. Window → TextMeshPro → Import TMP Essential Resources

---

## 2026-04-06 — Unity 에서 한글이 □ 로 표시

**증상**
한글 텍스트가 모두 □ 로 보임.

**원인**
TMP Essentials 에 영문 SDF 만 있음, 한글 글리프 없음.

**해결**
1. `C:\Windows\Fonts\malgun.ttf` 를 Unity Assets 로 복사
2. Window → TextMeshPro → Font Asset Creator → Dynamic SDF 생성
3. TMP Settings → Default Font Asset = malgun SDF

---

## 2026-04-06 — Windows 8080 포트 충돌

**증상**
`kubectl port-forward ... 8080:80` → 포트 사용 중 거부.

**원인**
Windows 가 8080 을 reserved 로 잡고 있음.

**해결**
다른 포트 사용 (예: 18080, 18081).

---

## 2026-04-06 — fiberprometheus v2.10.1 not found

**증상**
`go mod download` → version not found.

**해결**
`v2.17.0` 으로 올림.

---

## 2026-04-06 — Go 1.25 자동 bump

**증상**
goose 의존성이 Go 1.25 요구.

**해결**
- Dockerfile `FROM golang:1.25-alpine`
- `.github/workflows/server-ci.yaml` `go-version: '1.25'`
- `go.mod` `go 1.25`

---

## 2026-04-07 — AddressableBundleLoader CS0815

**증상**
```
Assets\Scripts\Core\Bundles\AddressableBundleLoader.cs(44,21):
  error CS0815: Cannot assign void to an implicitly-typed variable
```

**원인**
Addressables 2.x + UniTask 조합에서 `AsyncOperationHandle<T>.ToUniTask()` 확장이 void 반환 시그니처로 resolve 됨.

**해결**
```csharp
// 변경 전
var result = await op.ToUniTask();
return result;

// 변경 후
await op.Task;
if (op.Status != AsyncOperationStatus.Succeeded)
    throw new System.Exception($"LoadAssetAsync failed: {address}");
return op.Result;
```

**재발 방지**
- 제네릭 `AsyncOperationHandle<T>` 는 `op.Task` + `op.Result` 패턴 사용
- 비제네릭 `AsyncOperationHandle` 만 `await op.ToUniTask();` (반환값 안 받을 때)

---

## 2026-04-07 — Burst: Failed to resolve ShortGeta.Tests.PlayMode

**증상**
```
Mono.Cecil.AssemblyResolutionException:
  Failed to resolve assembly: 'ShortGeta.Tests.PlayMode, ...'
```

**원인**
다른 컴파일 에러 (CS0815) 때문에 Runtime asmdef 가 빌드 안 됨 → 의존하는 PlayMode dll 도 못 만듦 → Burst 가 못 찾음. **연쇄 에러**.

**해결**
앞선 컴파일 에러를 먼저 고친다. 이 에러 자체는 fix 불필요.

---

## 2026-04-07 — Addressables Editor "Invalid path settings.json"

**증상**
```
System.Exception: Invalid path in TextDataProvider :
  'Library/com.unity.addressables/aa/Windows/settings.json'.
RuntimeData is null.
Unable to load runtime data at location ...
```

**원인**
Addressables 가 한 번도 빌드 안 된 상태에서 Play Mode Script 가 빌드된 settings.json 을 찾으려 함.

**해결** (둘 중 하나)
- (a) **Window → Asset Management → Addressables → Groups → Play Mode Script ▼ → Use Asset Database (fastest)** ← 권장
- (b) Build → New Build → Default Build Script 한 번 실행

**재발 방지**
- 신규 개발자 셋업 가이드에 Play Mode Script = "Use Asset Database" 명시

---

## 2026-04-07 — TMP enableWordWrapping CS0618

**증상**
```
warning CS0618: 'TMP_Text.enableWordWrapping' is obsolete.
  Please use the textWrappingMode property instead.
```

**해결**
```csharp
// 변경 전
t.enableWordWrapping = true;
// 변경 후
t.textWrappingMode = TextWrappingModes.Normal;
```

---

## 2026-04-07 — ops-tool CI: cache: npm fail (no lock)

**증상**
```
##[error]Some specified paths were not resolved, unable to cache dependencies.
```

**원인**
`actions/setup-node@v4` 의 `cache: npm` + `cache-dependency-path: package-lock.json` 인데 lock 파일이 없음.

**해결**
workflow 에서 cache 옵션 제거:
```yaml
- uses: actions/setup-node@v4
  with:
    node-version: '22'
    # cache: npm  ← 삭제
```

**재발 방지**
- `package-lock.json` 이 없으면 cache 옵션 자체를 사용하지 않는다
- 또는 첫 install 한 번 돌려서 lock 파일 commit

---

## 2026-04-07 — ops-tool Dockerfile 빌드 실패

**증상 1**: `npm install --omit=dev=false` 문법 오류
**증상 2**: `COPY --from=build /app/public ./public` 에서 public/ 디렉토리 없음

**해결**
```dockerfile
RUN npm install --no-audit --no-fund --legacy-peer-deps
# public 디렉토리는 존재할 때만 COPY
```

---

## 2026-04-07 — ops-tool ERESOLVE next 15.0.3 vs react 19

**증상**
```
npm error peer react@"^18.2.0 || 19.0.0-rc-..." from next@15.0.3
```

**원인**
Next 15.0.3 의 peer dep 이 react 19 stable 을 받지 않음.

**해결**
- `next` 를 `15.1.6` 으로 올림
- 추가 안전망: `npm install --legacy-peer-deps`

---

## 2026-04-07 — Next.js standalone fetch "Provisional headers"

**증상**
브라우저에서 ops-tool 로그인 페이지 자체가 안 뜸. Network 탭에 `login` 만 빨간색.

**원인**
Next.js standalone 의 server.js 가 컨테이너 내부에서 `localhost:3000` 에만 바인딩.
port-forward 가 닿지 못함.

**해결**
deployment.yaml + Dockerfile 에 env 추가:
```yaml
- name: HOSTNAME
  value: "0.0.0.0"
- name: PORT
  value: "3000"
```

**재발 방지**
- Next.js standalone 컨테이너는 항상 `HOSTNAME=0.0.0.0` 명시

---

## 2026-04-07 — NEXT_PUBLIC_API_BASE 가 클러스터 내부 DNS

**증상**
ops-tool 로그인 시 fetch 가 `http://shortgeta-server.shortgeta-dev/v1/admin/login` 으로 가는데 브라우저에서 접근 불가.

**원인**
`NEXT_PUBLIC_*` 는 빌드 시점에 인라인됨. 클러스터 내부 DNS 는 브라우저에서 resolve 안 됨.

**해결**
- dev 임시: `value: "http://localhost:18081/v1"` (port-forward 호환)
- 운영: 동적 base URL (window.location 기반) 으로 lib/api.ts 리팩토링 필요

**재발 방지**
- 브라우저에서 호출하는 API URL 은 빌드 시점이 아닌 런타임 결정 권장

---

## 2026-04-07 — postgres deploy not found

**증상**
```
kubectl exec deploy/postgres ... → Error: deployments.apps "postgres" not found
```

**원인**
Postgres 는 Deployment 가 아닌 **StatefulSet** `shortgeta-postgres` (pod: `shortgeta-postgres-0`).

**해결**
```bash
kubectl exec shortgeta-postgres-0 -- psql ...
```

---

## 2026-04-07 — gh CLI: "not a git repository"

**증상**
`gh run list` → `failed to determine base repo`

**원인**
`gh` 는 현재 디렉토리의 git remote 에서 repo 추론. Hamin home 같은 곳에서는 못 찾음.

**해결**
`-R gensdeis/SGT` 옵션 명시 또는 `cd F:\SGT` 후 실행.

---

## 2026-04-07 — PowerShell curl 은 Invoke-WebRequest alias

**증상**
```
curl -X OPTIONS ... -i
→ Invoke-WebRequest : 'InFile' 매개 변수 ...
```

**원인**
PowerShell `curl` 은 `Invoke-WebRequest` alias 로 옵션 다름.

**해결**
`curl.exe` 명시 사용:
```powershell
curl.exe -X POST http://localhost:18081/v1/admin/login `
  -H "Content-Type: application/json" `
  -d "{\"login\":\"admin\",\"password\":\"xxx\"}"
```

---

## 2026-04-07 — goose: missing migrations before current version

**증상**
```
auto-migrate failed: goose up: error:
  found 1 missing migrations before current version 20260409120001:
    version 20260408130001: db/migrations/20260408130001_favorites.sql
```

**원인**
새 마이그레이션 timestamp 가 이미 적용된 마이그레이션보다 작음. goose 는 기본적으로 out-of-order 거부.

**해결**
파일명을 더 큰 timestamp 로 rename:
```bash
git mv 20260408130001_favorites.sql 20260410120001_favorites.sql
```

**재발 방지**
- 새 migration 은 항상 **현재 가장 큰 timestamp + 1** 로 작성
- 또는 `goose.WithAllowMissing()` 옵션 추가 (덜 안전)

---

## 2026-04-08 — Unity client "Cannot connect to destination host"

**증상**
```
Cysharp.Threading.Tasks.UnityWebRequestException: Cannot connect to destination host
  at ApiClient.PostJsonAsync ... AuthApi.LoginByDeviceAsync ...
```
Editor ▶ Play 시 device login 단계에서 실패.

**원인**
보통 둘 중 하나:
1. `kubectl port-forward svc/shortgeta-server 18081:80` 가 안 떠 있음
2. server pod 가 CrashLoopBackOff (migration 실패 등)

**해결**
```powershell
# 1) port-forward 살리기 (별도 터미널)
kubectl -n shortgeta-dev port-forward svc/shortgeta-server 18081:80

# 2) 검증
curl.exe http://localhost:18081/health

# 3) pod 상태
kubectl -n shortgeta-dev get pods -l app=shortgeta-server
```

**재발 방지**
- Editor Play 전에 `curl.exe http://localhost:18081/health` 1초 체크 습관
- ServerConfig-Dev.asset 의 BaseUrl 이 `localhost:18081` 인지 확인

---

## 2026-04-08 — Editor 에서 PC UI 가 떠서 진행 안됨

**증상**
Editor Game View 가 가로 비율일 때 PcHomeController placeholder 화면("PC UI — 곧 나옵니다") 만 보이고 시작 버튼 없음.

**원인**
`PlatformDetector` 가 `Screen.height > Screen.width` 면 Mobile, 아니면 PC 로 판단. Editor Game View 가로 비율이면 PC.

**해결**
Editor 매크로 분기 추가:
```csharp
#if UNITY_EDITOR
return LayoutMode.Mobile; // Editor 기본은 Mobile (Android 타겟)
#elif UNITY_ANDROID || UNITY_IOS
return LayoutMode.Mobile;
#else
...
#endif
```

PC 보고싶으면 `PlatformDetector.EditorOverride = LayoutOverride.ForcePC;` 로 강제.

---

## 2026-04-08 — Addressables: Expecting binary catalog but got json

**증상**
```
System.Exception: Expecting to load catalogs in binary format
  but the catalog provided is in .json format.
  To load it enable Addressable Asset Settings > Catalog > Enable Json Catalog.
```

**원인**
Addressables 2.x 가 기본 binary catalog 인데 서버가 보내는 catalog 가 .json 포맷.

**해결** (사용자 작업, 둘 중 하나)
- (a) Window → Asset Management → Addressables → Settings → **Catalog → Enable Json Catalog** ✅
- (b) 서버 catalog 를 binary 로 재빌드 (Addressables Build 시 Json 옵션 끄기)

---

## 2026-04-08 — Addressables: InvalidKeyException demo/hello

**증상**
```
UnityEngine.AddressableAssets.InvalidKeyException:
  No Location found for Key=demo/hello
```

**원인**
`Assets/Demo/HelloAddressable.txt` 가 Addressable 마크 안 된 상태. (또는 demo 디렉토리 없음)

**영향**
없음. fallback try/catch 에서 warning 만 찍고 진행.

**해결** (선택)
- Project → `Assets/Demo/HelloAddressable.txt` → Inspector → Addressable ✅ → address `demo/hello`
- 또는 BootstrapController 에서 demo 자산 로드 부분 제거

---

## 2026-04-08 — Addressables 2.10.0 ProjectConfigData CS0426

**증상**
```
Library\PackageCache\com.unity.addressables@a1aed830bc3c\Editor\Settings\
  ProjectConfigData.cs(13,80): error CS0426:
  The type name 'ResourceLocator' does not exist in the type 'ContentCatalogData'
```
Play 가 안됨 (컴파일 자체 실패).

**원인**
Addressables 2.10.0 의 알려진 내부 빌드 이슈. 일부 internal API 가
잘못 export 되어 있음. PackageCache 정리만으로는 해결 안 됨.

**해결**
1. Unity Editor 완전히 종료
2. `Packages/manifest.json` 의 Addressables 버전 다운그레이드:
   ```json
   "com.unity.addressables": "2.5.0",
   ```
3. PackageCache + ScriptAssemblies + packages-lock.json 삭제:
   ```powershell
   Remove-Item -Recurse -Force F:\SGT\client\Library\PackageCache
   Remove-Item -Recurse -Force F:\SGT\client\Library\ScriptAssemblies
   Remove-Item -Force F:\SGT\client\Packages\packages-lock.json
   ```
4. Unity Hub 에서 다시 열기 → 패키지 재해결

**재발 방지**
- Addressables 2.5.0 ~ 2.10.x 는 Unity 6 + 일부 패키지 조합에서 오류
- **2.4.4 가 가장 안정** (검증됨)
- 다음 후보: 2.3.16, 2.2.2

**Note**: 2.5.0 은 또 다른 에러 (`FormerlySerializedAs` not found) 발생.
2.4.4 로 직접 가는 것을 권장.

---

## 2026-04-08 — TMP Dynamic SDF 한글 글리프에 □ 박스 덧씌워짐

**증상**
한글 텍스트는 보이는데 각 글자 주위에 □ 사각형이 같이 렌더됨.

**원인**
Font Asset Creator 에서 이전에 Generate 할 때 **Padding = 0** 으로 생성.
Dynamic 모드 전환 후에도 이 Padding 값이 그대로라 atlas 에서 글리프 가장자리가 서로 침범해 사각형 artifact.

**해결**
1. `Assets/Fonts/malgun SDF.asset` 클릭
2. Inspector → Generation Settings → **Padding**: `0` → **`5`**
3. Inspector 상단 **Update Atlas Texture** 버튼 클릭 (중요 — 안 누르면 반영 안됨)
4. ▶ Play

**재발 방지**
- Font Asset 생성 시 Padding 은 최소 5 이상
- Dynamic 모드 SDF 는 글리프 재생성 시 `Update Atlas Texture` 필수

---

> ⬇ 새 항목은 여기 아래에 추가
