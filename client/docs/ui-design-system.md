# 숏게타 UI 디자인 시스템

shortgeta-plan-v1.3.html 의 디자인을 코드로 옮긴 토큰 + 헬퍼 모음.

## 토큰

`Assets/Scripts/Core/UI/DesignTokens.cs` 만 참조. 직접 hex/픽셀 금지.

| 카테고리 | 토큰 | 값 |
|---|---|---|
| 배경 | `Bg` | `#0c0e13` |
| 카드 | `Surface` | `#14161d` |
| 보조 | `Surface2` | `#1b1e28` |
| 테두리 | `Border` | `#2a2e3a` |
| 텍스트 | `Text` | `#e2e4ea` |
| 보조텍스트 | `TextDim` | `#8b8fa3` |
| 강조 | `Accent` | `#5eead4` (teal) |
| 다크강조 | `AccentDark` | `#0F6E56` |
| CTA | `PrimaryCTA` | `#9FE1CB` |
| CTA 텍스트 | `OnPrimary` | `#04342C` |
| 네비 | `NavBg` | `#111318` |
| 퀵카드 | `QuickBg` | `#085041` |
| 골드 | `Gold` | `#fbbf24` |

폰트 사이즈 (720x1280 reference):
`FontTitle=64, FontH2=44, FontBody=28, FontCaption=22, FontTag=18`

## 헬퍼

`Assets/Scripts/Core/UI/UIBuilder.cs`

- `Panel(parent, name, anchorMin, anchorMax, color)` — 사각 패널
- `Label(parent, text, fontSize, color, align, ...)` — TMP 텍스트
- `Button(parent, name, bg, fg, label, fontSize, anchorMin, anchorMax, onClick)` — 버튼
- `Tag(parent, text)` — Surface2 bg + dim text 태그 라벨

## Stage 1 적용 범위

- ✅ 토큰 + UIBuilder
- ✅ 홈 화면 재작성 (BootstrapController.ShowHome)
  - 상단바 (로고 + 코인)
  - 퀵 시작 카드
  - 게임 카드 ScrollView
  - 하단 탭 4개

## Stage 2 (후속)
- 9-slice rounded sprite (현재는 사각형)
- 카운트다운 + 게임 타이틀 카드 + HUD
- 결과 화면 임팩트 + 랭킹 표시
- 인기/보관함 탭 실구현
- SafeArea 대응

## 한글/이모지

- malgun.ttf SDF 가 Default Font Asset 으로 등록되어 있음
- 이모지가 □ 로 보이면 NotoColorEmoji 동적 SDF fallback 추가 필요 (Stage 3)
