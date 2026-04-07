# 하이라이트 녹화 native plugin 빌드 가이드 (Iter 2B')

이 문서는 Android / iOS native 측 highlight recording 코드를 빌드해서 Unity 에
통합하는 절차를 설명합니다.

> **상태**: Iter 2B' 에서 모든 native 소스 (Java / Objective-C) + C# bridge 를
> 작성했습니다. 단 작업 환경에 Android Studio / Xcode 가 없어서 실제 빌드 + 실기기
> 검증은 사용자가 수행해야 합니다.

---

## Android (MediaProjection + ImageReader)

### 1. 사전 조건
- Android Studio 2024+ (Hedgehog 이상)
- Android SDK 34 + Build Tools 34
- JDK 17

### 2. 소스 위치
```
client/Assets/Plugins/Android/HighlightRecording/
├── build.gradle
└── src/main/
    ├── AndroidManifest.xml
    └── java/com/shortgeta/recording/HighlightRecorder.java
```

### 3. AAR 빌드

**옵션 A — Android Studio**
1. Android Studio 실행
2. **Open** → `client/Assets/Plugins/Android/HighlightRecording/` 폴더 선택
3. Gradle sync 완료 대기
4. 우측 Gradle 패널 → `HighlightRecording → Tasks → build → assembleRelease`
5. 결과: `build/outputs/aar/HighlightRecording-release.aar`

**옵션 B — 커맨드라인**
```bash
cd client/Assets/Plugins/Android/HighlightRecording
./gradlew assembleRelease
```

### 4. Unity 에 통합
- 빌드된 `HighlightRecording-release.aar` 를 다음 경로에 복사:
  ```
  client/Assets/Plugins/Android/HighlightRecording.aar
  ```
- 기존 `HighlightRecording/` 소스 디렉토리는 그대로 두거나 .gitignore 처리
- Unity 가 자동으로 AAR 를 인식 → APK 빌드 시 포함

### 5. AndroidManifest 머지 확인
- `client/Assets/Plugins/Android/AndroidManifest.xml` 가 자동 머지되어
  `FOREGROUND_SERVICE_MEDIA_PROJECTION` 권한이 최종 APK 에 포함되어야 함

### 6. APK 빌드 + 실기기 테스트
- Unity → File → Build Settings → Android → Switch Platform
- Player Settings → Other Settings → Min API 24, Target API 34, IL2CPP, ARM64
- Build → APK
- 실기기 설치 → 미니게임 플레이
- 첫 호출 시 시스템 권한 다이얼로그 ("화면 캡처 시작?") 승인
- Console (`adb logcat -s ShortGetaRecorder`) 에서 로그 확인:
  ```
  ShortGetaRecorder: startRecording tag=frog_catch_v1-...
  ShortGetaRecorder: stopRecording, frames=30
  ShortGetaRecorder: flushLastClipPath → /sdcard/Android/data/com.shortgeta.app/files/highlights/...
  ```

### 7. 알려진 한계 (Iter 2B' MVP)
- MediaProjection 권한 요청 흐름이 단순화되어 있음 — `RecordingPermissionActivity`
  helper 는 후속 (Iter 2B''')
- Foreground Service 미구현 — Android 14+ 에서 권한은 있으나 service 자체는 없어
  화면 OFF 시 recording 안 됨
- jpeg 시퀀스만 (MP4 인코딩 후속)
- 음성 OFF

---

## iOS (ReplayKit RPScreenRecorder)

### 1. 사전 조건
- macOS + Xcode 15+
- iOS 11 이상 디바이스
- Apple Developer 계정 (실기기 설치 시)

### 2. 소스 위치
```
client/Assets/Plugins/iOS/
├── HighlightRecorder.h
└── HighlightRecorder.mm
```

### 3. 빌드 (사용자가 macOS 에서)
1. Unity Hub → 클라이언트 프로젝트 열기 (macOS)
2. File → Build Settings → iOS → Switch Platform
3. Player Settings → Other Settings:
   - Bundle Identifier: `com.shortgeta.app`
   - Target minimum iOS Version: 11
   - Architecture: ARM64
4. **Build** → 출력 디렉토리 선택 → Xcode 프로젝트 export
5. Xcode 자동 실행

### 4. Xcode 설정
- 좌측 navigator → Unity-iPhone → TARGETS → Unity-iPhone
- **Signing & Capabilities** → Team 선택
- **Info.plist** 에 다음 키 추가:
  ```xml
  <key>NSMicrophoneUsageDescription</key>
  <string>(녹화 옵션에서 마이크 사용 — 현재는 비활성)</string>
  ```
- ReplayKit 은 시스템 framework 라 별도 추가 불필요. 단 import 누락 시
  Build Phases → Link Binary With Libraries 에 `ReplayKit.framework` 수동 추가

### 5. 빌드 + 실기기 테스트
- 실기기 연결 → Cmd+R
- 미니게임 플레이 → 첫 호출 시 시스템 다이얼로그 (화면 녹화 권한)
- Xcode console 에서:
  ```
  [ShortGeta] iOS start tag=frog_catch_v1-...
  [ShortGeta] flush → /var/mobile/Containers/Data/Application/.../tmp/highlights/...
  ```

### 6. 알려진 한계 (Iter 2B' MVP)
- AVAssetWriter export 가 동기 wait (5초 timeout) — UI 멈춤 가능
- 권한 거부 시 UI 안내 없음 — 단순 NULL 반환
- 음성 OFF
- 워터마크 미적용 (Editor 에서는 적용)

---

## 공통

### Unity bridge 분기 동작
`BootstrapController.BuildRecordingService()` 가 RuntimePlatform 으로 분기:
- WindowsEditor / Standalone → `EditorRecordingService` (PNG 시퀀스 + 워터마크)
- Android → `AndroidRecordingService` (Java AAR 호출)
- iPhonePlayer → `IosRecordingService` (Objective-C P/Invoke)
- 기타 → `NativeStubRecordingService` (no-op)

### 검증 시 Console 키워드
- Editor: `[Recording] EditorRecordingService ready`
- Android: `[Recording] AndroidRecordingService ready` 또는 `init failed (AAR not installed?)`
- iOS: `[Recording] iOS start tag=...`

### 후속 (Iter 2B''')
- Android Foreground Service + RecordingPermissionActivity helper
- MP4 인코딩 (Android: MediaMuxer / iOS: 이미 AVAssetWriter 사용 중)
- 원탭 공유 (Android Intent ACTION_SEND, iOS UIActivityViewController)
- 워터마크 native side 적용 (현재는 Editor 만)
- 음성 캡처 옵션
- 10MB 한도 자동 압축
