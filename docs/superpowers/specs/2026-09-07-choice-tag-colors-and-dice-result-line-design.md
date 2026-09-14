# 선택지 태그 색상 + 주사위 결과 줄 설계

## 배경

선택지 + 주사위 방향 판정 시스템(2026-09-04 스펙)이 들어오면서, 매 턴 나레이션 끝에
`1. [안전] …` 형식의 선택지 목록이 중앙 나레이션 버블로 표시된다. 위험/무모 선택을 하면
주사위 두 개를 굴려 등급을 정하고, 그 결과는 `DiceRollOverlayView`에서 잠깐 라벨로 보였다가
사라진다.

두 가지가 아쉽다:

- 선택지의 위험도(`[안전]`/`[위험]`/`[무모]`)가 전부 같은 색이라 한눈에 안 들어온다.
- 주사위 결과(`8 · 부분 성공`)가 오버레이가 사라지면 채팅에 남지 않아, 플레이어가 "방금
  뭐가 나왔더라"를 다시 확인할 수 없다.

## 목표

- 선택지 목록에서 `[안전]` / `[위험]` / `[무모]` **태그 라벨만** 각각 초록 / 노랑(골드) /
  빨강으로 표시한다. 본문 행동 텍스트는 기본색 유지.
- 위험·무모 선택 시, 주사위 애니메이션이 끝난 뒤 `⚂ ⚄  8 · 부분 성공` 형식의 결과를
  **별도의 가운데 정렬 나레이션 버블**로 보여준 뒤 나레이션 버블이 이어진다. 글리프는
  주사위 애니메이션에 쓰는 것과 동일한 `⚀`~`⚅`. (버블 간 자연스러운 간격으로 "결과 →
  한 줄 띄고 → 나레이션" 레이아웃이 나온다.)
- 두 표시 모두 **화면에만** 반영한다. 대화 히스토리와 LLM 프롬프트에는 평문만 저장하며,
  LLM은 계속 숫자가 아니라 방향 문장만 본다 (2026-09-04 스펙의 설계 의도 유지).

## 범위

### 추가

- `Assets/02_Script/Runtime/Core/DiceFaces.cs`
  - `static string Glyph(int value)` — 눈 값(1~6) → 글리프 문자열. 범위를 벗어나면 클램프.
  - 내부 배열 `{ "⚀", "⚁", "⚂", "⚃", "⚄", "⚅" }`. 주사위 눈 글리프의 표준 소스.
  - `DiceRollAnimation`은 씬/프리팹에 직렬화된 `faces` 값을 그대로 쓰므로 건드리지 않는다.
    (두 곳이 같은 글리프 집합을 쓴다는 전제. 6글자 상수라 중복을 감수한다.)
- `Assets/02_Script/Runtime/Systems/ChoiceListFormatter.cs`
  - `ChatController`에 `private static` 로 있던 `FormatChoices` / `RiskTag` 순수 문자열
    로직을 이 정적 클래스로 옮긴다 (`DiceDirectionText`와 같은 위치·성격).
  - `static string Plain(IReadOnlyList<Choice> choices)` — `"1. [안전] 뒤로 물러난다\n2.
    [위험] 문을 연다"` 형식. 대화 히스토리 저장용. 기존 `FormatChoices` 출력과 동일.
  - `static string Colored(IReadOnlyList<Choice> choices)` — `Plain`과 같되 각 줄의
    `[안전]`/`[위험]`/`[무모]` 라벨만 `<color=#…>[위험]</color>` 로 감싼다. 번호·본문은
    감싸지 않는다. 화면 표시용.
  - 색 상수(클래스 상단, 조정 쉽게):
    - `SafeColor = "#2E9E5B"` (초록)
    - `RiskyColor = "#C88A1E"` (진노랑/골드 — 순수 노랑은 밝은 배경에서 안 보여 어둡게)
    - `RecklessColor = "#D33A3A"` (빨강)
  - `ChoiceRisk` → 라벨(`[안전]` 등)과 색을 매핑하는 내부 헬퍼.
- `Assets/02_Script/Tests/EditMode/DiceFacesTests.cs`
  - `Glyph(1)` == `"⚀"`, `Glyph(6)` == `"⚅"`, `Glyph(0)`/`Glyph(7)` 클램프 확인.
- `Assets/02_Script/Tests/EditMode/ChoiceListFormatterTests.cs`
  - `Plain` 이 태그 텍스트를 색 없이 그대로 내는지 (`<color` 미포함).
  - `Colored` 가 각 줄에서 해당 risk 색으로 라벨만 감싸는지, 본문 행동 텍스트는 색 태그
    밖에 있는지.
  - risk 3종 → 색 매핑 (`Safe`→초록, `Risky`→골드, `Reckless`→빨강).
  - 빈 리스트 → 빈 문자열.

### 변경

- `Assets/02_Script/Runtime/Core/ChatMessage.cs`
  - `public string DisplayText;` 필드 추가. 생성자에는 넣지 않고 객체 초기화자로 설정.
  - 의미: `DisplayText` 가 `null`/빈 문자열이면 화면에 `Text` 를 그대로 쓴다. **프롬프트와
    대화 히스토리는 언제나 `Text` 를 쓴다** — `DisplayText` 는 순수 표시용.
- `Assets/02_Script/Runtime/UI/ChatBubbleListView.cs`
  - `TryPlayNext` 의 `_current.Play(message.Sender, message.Text, message.SenderName)` 를
    `_current.Play(message.Sender, string.IsNullOrEmpty(message.DisplayText) ? message.Text
    : message.DisplayText, message.SenderName)` 로 변경. 그 외 로직(플레이어 판별, 큐,
    입력 상태)은 불변.
- `Assets/02_Script/Runtime/UI/ChatController.cs`
  - `FormatChoices` / `RiskTag` 제거 → `ChoiceListFormatter` 로 위임.
  - 선택지 목록 메시지 생성부(현재 152~158행):
    - `choicesMessage.Text = ChoiceListFormatter.Plain(_lastChoices)`
    - `choicesMessage.DisplayText = ChoiceListFormatter.Colored(_lastChoices)`
  - `ProceedWithTurn(Choice selectedChoice, string directionHint)` →
    `ProceedWithTurn(Choice selectedChoice, string directionHint, string diceResultLine = null)`.
  - `SendPlayerMessage` 의 위험/무모 분기에서 방향 힌트를 계산한 뒤 결과 줄도 만든다:
    - `diceResultLine = $"{DiceFaces.Glyph(roll.A)} {DiceFaces.Glyph(roll.B)}  {roll.Total}
      · {DiceDirectionText.ShortLabel(outcome)}"`
    - `OnDiceRollRequested` 구독 시: continuation 을 `() => ProceedWithTurn(selectedChoice,
      directionHint, diceResultLine)` 로.
    - 미구독(테스트 등) 즉시 진행 시에도 `ProceedWithTurn(selectedChoice, directionHint,
      diceResultLine)`.
    - 안전 선택 / 자유 입력 분기는 `diceResultLine` 없이 기존대로 (`null`).
  - `ProceedWithTurn` 시작부에서 `diceResultLine` 이 있으면 `ChatSender.Narration` 메시지를
    만들어 `OnMessageAdded` 로만 방출한다 (`AppendConversationMessage` 하지 않음 — 화면
    표시 전용). LLM 응답을 기다리는 동안 먼저 뜨므로 즉각적인 피드백이 된다.
  - `onSuccess` 의 나레이션 메시지는 기존과 동일 (`Text = response.Narration`).
  - `DiceRollOverlayView` 에 넘기는 잠깐 뜨는 라벨(`outcomeLabel`, 현재 `"{A} + {B} =
    {Total} · {ShortLabel}"`)은 그대로 둔다 — 오버레이는 이미 슬롯에 글리프를 찍으므로.

### 이번 범위에 포함하지 않음 (later)

- 선택지 줄 **전체** 색칠, 위험도 아이콘, 선택지 버튼 UI.
- 자유 입력(예: `"일번할래"`)에 대한 주사위 판정 및 결과 줄 — 지금은 숫자만 정확히 입력한
  경우에만 굴림/결과 줄이 나온다.
- 합쳐진 나레이션 버블에서 주사위 줄을 타이핑 없이 즉시 표시하는 최적화. (지금은 버블 전체가
  한 글자씩 나오며, 주사위 줄 ~13자는 약 0.3초 소요 — 거슬리면 그때)
- 색상값 테마화 / 다크·라이트 대응. 지금은 `ChoiceListFormatter` 상수 하나뿐.

## 데이터 흐름

### 선택지 색상

1. `ProceedWithTurn.onSuccess`: `_lastChoices = response.Choices` 후, 엔딩 아니고 선택지가
   있으면 선택지 목록 메시지를 만든다.
2. `Text = ChoiceListFormatter.Plain(_lastChoices)` (색 없음) → `DataManager` 에 저장 →
   다음 턴 프롬프트의 최근 히스토리에 이 평문이 들어간다.
3. `DisplayText = ChoiceListFormatter.Colored(_lastChoices)` → `OnMessageAdded` →
   `ChatBubbleListView` 가 `DisplayText` 를 골라 `ChatBubble` 에 넘김 → TMP 리치텍스트로
   태그 라벨만 색이 입혀져 렌더된다.

### 주사위 결과 줄

1. `SendPlayerMessage`: 위험/무모 선택 → `DiceRoll.Roll()` → `Bucket` → `outcome`.
   `directionHint`(프롬프트용 방향 문장)와 `diceResultLine`(화면용, 글리프 포함)을 둘 다
   만든다.
2. `OnDiceRollRequested.Invoke(roll, outcomeLabel, () => ProceedWithTurn(choice,
   directionHint, diceResultLine))`. 오버레이가 주사위를 하나씩 굴리고, 정착하면
   continuation 호출.
3. `ProceedWithTurn`: `directionHint` 는 `extraHint` 에 섞여 프롬프트로 (LLM 은 방향
   문장만 봄). `diceResultLine` 은 프롬프트에 넣지 않고 클로저로 `onSuccess` 까지 전달.
4. `ProceedWithTurn` 시작부: `diceResultLine` 이 있으면 별도 `Narration` 버블을
   `OnMessageAdded` 로만 방출 (히스토리 저장 안 함). 이어서 `onSuccess` 의 나레이션 버블은
   `Text = response.Narration` 로 기존과 동일하게 저장/표시. `DataManager` 에는 주사위 줄이
   안 남으므로 다음 턴 LLM 은 숫자를 못 본다.

## 테스트 영향

- 신규: `DiceFacesTests`, `ChoiceListFormatterTests` (위 "추가" 참조).
- 기존 `GeminiProviderChoicesTests`, `ChoiceInputParserTests`, `DiceRollTests`,
  `DiceDirectionTextTests`, `PromptBuilderTests` — 스키마·파싱·방향 문장·프롬프트 텍스트가
  바뀌지 않으므로 영향 없음.
- `ChatController` / `ChatBubbleListView` 는 `DataManager.Instance` / 씬 오브젝트 의존이라
  단위 테스트가 어렵다. 합쳐진 나레이션 버블 표시, 태그 색 렌더, 히스토리에 평문만 저장되는
  것은 Play 모드 스모크 테스트로 확인한다. 순수 로직(글리프 매핑, 선택지 포맷)은 위 신규
  테스트로 커버한다.

## 폰트 확인

주사위 글리프(`⚀`~`⚅`, U+2680–U+2685)는 `Assets/03_Resource/DiceGlyphs SDF.asset` 가
`JayeonSans-Medium SDF` 의 폴백으로 등록돼 있어야 렌더된다 (2026-09-04 작업에서 추가됨).
구현 시 나레이션 버블 프리팹의 TMP 폰트가 `JayeonSans-Medium SDF` 이고 그 폴백 목록에
`DiceGlyphs SDF` 가 있는지 확인한다. 없으면 폴백에 추가한다.
