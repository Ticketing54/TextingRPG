# 선택지 + 주사위 방향 판정 시스템 설계

## 배경

현재 게임은 LLM 나레이터가 매 턴 플레이어의 자유 입력을 받아 결과를 서술한다. 결과의
성공/실패는 전적으로 LLM이 세계관과 확정된 사실만 보고 판단한다.

여기에 **주사위 층**을 얹는다. 매 턴 나레이션이 선택지 3~5개를 제시하고, 플레이어가 "위험한"
선택지를 고르면 **6면체 주사위 두 개**를 굴려 합(2~12)으로 **결과의 방향**을 정한다. 그 방향을
문장으로 번역해 프롬프트에 넣고, LLM은 그 방향대로 나레이션한다. 스탯 시스템은 없다.

기존 커밋 `270d9e8 stop LLM from accepting player-asserted outcomes as fact` / `7a773e9`의
프롬프트 방어와 방향이 일치한다 — 결과 방향의 권한을 LLM이 아니라 주사위가 가진다.

## 목표

- 매 턴 LLM이 선택지 3~5개를 제시하고, 각 선택지에 `안전` 또는 `위험` 태그를 붙인다.
- 플레이어가 안전한 선택을 하면 판정 없이 진행되지만 상황이 크게 바뀌지 않는다(정체).
- 위험한 선택을 하면 주사위 두 개를 굴려 합으로 5단계 등급을 정한다:
  2 대실패 / 3–6 실패 / 7–9 부분성공 / 10–11 성공 / 12 대성공. (2=1+1, 12=6+6이므로
  "더블 1 = 대실패, 더블 6 = 대성공"과 동일하다.) LLM에는 숫자가 아니라 방향 문장만 전달한다.
- 위험 선택 시 화면 중앙에 주사위 두 개가 굴러가는 애니메이션을 보여준 뒤에 LLM을 호출한다.
- 선택지 UI는 만들지 않는다. 나레이션 버블에 텍스트로 나열하고, 플레이어는 입력창에 숫자만
  입력한다. `trim` 후 정확히 `1`~`N`이면 선택으로, 아니면 자유 입력으로 처리한다.

## 범위

### 추가

- `Assets/02_Script/Runtime/LLM/Choice.cs`
  - `enum ChoiceRisk { Safe, Risky, Reckless }` (안전 / 위험 / 무모)
  - `class Choice { string Text; ChoiceRisk Risk; }`
- `Assets/02_Script/Runtime/Core/DiceRoll.cs`
  - `enum DiceOutcome { CriticalFailure, Failure, Partial, Success, CriticalSuccess }`
  - `readonly struct DiceRollResult { int A; int B; int Total => A + B; }`
  - `static DiceRollResult Roll()` — 실제 게임용, 주사위 두 개를 각각 `Random.Range(1,7)`
  - `static DiceOutcome Bucket(int total, bool reckless = false)` — 순수 함수.
    기본(위험): 2 / 3–6 / 7–9 / 10–11 / 12.
    무모(reckless): 2–3 / 4–6 / 7–8 / 9–10 / 11–12 — 크리티컬 구간이 넓고 중간이 좁다.
- `Assets/02_Script/Runtime/Systems/DiceDirectionText.cs`
  - `static string ForRisky(DiceOutcome)` — 위험 선택 결과를 프롬프트 문장으로 (5종)
  - `static string ForReckless(DiceOutcome)` — 무모 선택 결과를 프롬프트 문장으로 (5종, 위험보다
    성공·실패의 폭이 크다)
  - `static string ForSafe()` — 안전 선택을 프롬프트 문장으로
  - `static string ShortLabel(DiceOutcome)` — "대실패"/"실패"/"부분 성공"/"성공"/"대성공"
  - 문자열은 아래 "방향 문장" 표 참조
- `Assets/02_Script/Runtime/Core/ChoiceInputParser.cs`
  - `static int? Parse(string text, int choiceCount)` — `trim` 후 `1`~`choiceCount`
    정수면 0-based 인덱스, 아니면 `null`. `ChatController`에서 분리한 순수 함수.
- `Assets/02_Script/Runtime/UI/DiceRollAnimation.cs`
  - 주사위 한 개짜리 재사용 애니메이션. `Roll(int forcedResult = -1)` → 눈이 빠르게 교체되며
    점점 느려지다 정착, `OnRollComplete(int)` 발생. DOTween(코어 shortcut) 사용.
    `travelDistance`를 0으로 두면 제자리에서 회전+눈교체만 한다.
  - `string FaceGlyph(int value)` — 눈 값에 해당하는 글리프(결과를 다른 곳에 찍을 때).
- `Assets/02_Script/Runtime/UI/DiceRollOverlayView.cs`
  - `ChatController.OnDiceRollRequested`를 구독하는 화면 중앙 오버레이(MonoBehaviour).
    가운데의 `roller`(DiceRollAnimation 1개)로 주사위를 **하나씩 순서대로** 굴리고, 정착할
    때마다 결과 글리프를 아래 `landedSlots`(2칸)에 하나씩 찍는다(`DOPunchScale`로 팝).
    둘 다 끝나면 등급 라벨(`"3 + 5 = 8 · 부분 성공"`)을 보여준 뒤 `afterSettleDelay` 후 스스로
    숨으며 continuation을 호출한다. `CanvasGroup`(alpha / blocksRaycasts)로 표시·숨김 —
    자식은 항상 active여야 코루틴이 돈다. 버튼 등 수동 트리거는 없다.

### 변경

- `TurnResponse`: `List<Choice> Choices = new();` 추가.
- `GeminiProvider.BuildTurnRequestBody`: `responseSchema`에 `choices` 추가 —
  `ARRAY` of `OBJECT { text: STRING, risk: STRING (enum ["안전","위험","무모"]) }`. `required`에
  `choices` 포함.
- `GeminiProvider.ParseTurnResponse`: `choices` 배열을 `List<Choice>`로 파싱 (`ParseRisk`):
  `"위험"` → `Risky`, `"무모"` → `Reckless`, 그 외 → `Safe`.
- `PromptBuilder.BuildTurnPrompt`: 지시문에 추가 —
  - "이번 턴 나레이션 끝에, 플레이어가 다음에 취할 수 있는 행동을 choices에 3~5개 제시한다.
    가능하면 안전한 선택지와 위험한 선택지를 섞는다."
  - "risk가 `안전`인 선택은 반드시 성공하지만 상황을 크게 진전시키지 않으며, 때로는 기회를
    놓치게 한다. `위험`은 성패가 갈릴 수 있는 행동, `무모`는 위험보다도 더 크게 성패가 갈리는
    무리한 행동이다 (성공하면 큰 성과, 실패하면 큰 피해)."
  - `extraHint`(기존 파라미터)로 "플레이어 선택: {선택지 텍스트}" + 주사위 방향 문장을
    전달받아 그대로 이어붙인다 (별도 파라미터 추가 없음).
- `ChatController`:
  - 필드 `List<Choice> _lastChoices` + `bool _awaitingResponse`(응답 대기 중 재전송 차단).
  - `event Action<DiceRollResult, string, Action> OnDiceRollRequested` — (굴림 결과, 등급
    라벨, 애니메이션이 끝나면 부를 콜백). 구독자가 없으면 애니메이션 없이 즉시 진행.
  - `SendPlayerMessage(text)`:
    1. 가드: `_conversationEnded || _awaitingResponse`면 무시.
    2. `ChoiceInputParser.Parse(text, _lastChoices.Count)`가 인덱스면 → 해당 `Choice` 선택.
       `null`이면 자유 입력.
    3. 선택된 경우 플레이어 말풍선 텍스트를 그 `Choice.Text`로 치환해 기록/표시한다.
    4. `_awaitingResponse = true`.
    5. `Risky` / `Reckless` 선택 → `DiceRoll.Roll()`로 두 눈을 먼저 굴려두고
       (`reckless = Risk == Reckless`), `outcome = Bucket(Total, reckless)`,
       `directionHint = reckless ? ForReckless(outcome) : ForRisky(outcome)`,
       `OnDiceRollRequested.Invoke(roll, "{A} + {B} = {Total} · {ShortLabel}", () =>
       ProceedWithTurn(choice, directionHint))`. 구독자 없으면 즉시 `ProceedWithTurn`.
       `Safe` / 자유 입력 → 바로 `ProceedWithTurn`(방향 문장은 각각 `ForSafe()` / 빈 문자열).
  - `ProceedWithTurn(selectedChoice, directionHint)` (분리된 메서드):
    - 마무리 힌트(80/100턴) 계산 → `extraHint = ["플레이어 선택: {텍스트}"] + [방향 문장] +
      [마무리 힌트]` 조합 → `PromptBuilder.BuildTurnPrompt` → `provider.ContinueStory`.
    - `onSuccess`: `_awaitingResponse = false` → 나레이션 말풍선 → (NpcLine) NPC 말풍선 →
      `NewFacts` 반영 → `_lastChoices = response.Choices ?? new()` → 엔딩 아니고 선택지 있으면
      **선택지 목록을 별도 Narration 말풍선**으로 큐에 추가(`1. [안전] …` 형식, 대화 기록에도
      남김) → `isFinalTurn || IsEnding`이면 종료.
    - `onError`: `_awaitingResponse = false` → `OnError`.
  - `response.Choices`가 비었으면 `_lastChoices`는 빈 리스트가 되고, 다음 턴 입력은 전부 자유
    입력으로 처리된다. 개수가 3~5를 벗어나도 그대로 표시한다 (강제하지 않음).
- `ChatBootstrap`: `[SerializeField] DiceRollOverlayView diceRollOverlayView` 추가,
  `HandleNameConfirmed`에서 `diceRollOverlayView.Bind(_controller)`.

### 플레이어 이름 (2026-09-06 추가)

키워드 선택 다음, 게임 시작 전에 이름을 받는다. 이름은 필수(비면 시작 불가).

- `Assets/02_Script/Runtime/UI/PlayerNamePanel.cs` — `TMP_InputField` + 확인 버튼.
  입력이 비면 버튼 비활성, 확인 시 `OnNameConfirmed(string)` 발생 후 자기 숨김.
- `ChatBootstrap`: 흐름이 `키워드 확정 → PlayerNamePanel 표시 → 이름 확정 → ChatController 생성`
  으로 바뀜. `HandleKeywordsConfirmed`는 키워드만 저장하고 패널을 켜고,
  `HandleNameConfirmed(name)`가 provider/controller 생성·바인딩·`BeginAdventure`를 한다.
- `ChatController(provider, worldDescription, playerName)` — 이름을 보관.
- `PromptBuilder.BuildStoryOutlinePrompt(worldDescription, playerName)` /
  `BuildTurnPrompt(outline, facts, playerName, extraHint)` — 프롬프트 맨 위에
  `[플레이어 이름] {name}` + "주인공을 이 이름으로 부른다" 지시를 넣는다.
- 씬: `Canvas/ChatUI/PlayerNamePanel` (KeywordIntroPanel 다음 형제, 기본 비활성) —
  제목 라벨 + `NameInput`(MessageInputField 복제) + `ConfirmButton`(StartButton 복제).
- 테스트: `PromptBuilderTests.cs` — 두 프롬프트가 이름을 담는지.
- 씬 `PlayScene.unity`: `Canvas/DiceRollOverlay` 추가 — 루트(항상 active, `DiceRollOverlayView`) /
  `Visual`(`CanvasGroup`, 항상 active) / `Backdrop`(반투명, raycast 차단) /
  `Roller`(TMP + `DiceRollAnimation`, 왼쪽에서 오른쪽으로 굴러감) / `Slot0`·`Slot1`(TMP,
  아래 한 줄) / `OutcomeLabel`.

### 이번 범위에 포함하지 않음 (later)

- 선택지 버튼 UI / 안전·위험 색·아이콘. (타이핑이 거슬리면 그때)
- 자유 입력에 대한 주사위 판정. (지금은 방향 문장 없이 그대로 LLM에 전달)
- 위험 선택지별 난이도 수치 / 주사위 보정. (위험/무모는 각각 고정 규칙)
- 오프닝 나레이션에 선택지 붙이기. (첫 행동은 자유 입력, 턴 1 응답부터 선택지 등장)

## 방향 문장

| 상황 | 프롬프트에 들어가는 문장 |
|---|---|
| 위험 · 2 (CriticalFailure) | "플레이어의 이번 시도는 완전히 어긋나 심각한 문제나 큰 대가가 생긴다. 단, 이야기가 여기서 끝나버리지는 않게 한다." |
| 위험 · 3–6 (Failure) | "플레이어의 이번 시도는 뜻대로 되지 않거나 예상치 못한 문제가 생긴다. 단, 이야기가 막다른 길로 가지는 않게 한다." |
| 위험 · 7–9 (Partial) | "플레이어의 이번 시도는 어느 정도 통하지만, 대가나 새로운 복잡함이 따른다." |
| 위험 · 10–11 (Success) | "플레이어의 이번 시도는 순조롭게 통한다." |
| 위험 · 12 (CriticalSuccess) | "플레이어의 이번 시도는 기대 이상으로 완벽하게 통하고, 뜻밖의 이득까지 따라온다." |
| 안전 | "플레이어는 안전한 선택을 했다. 무리 없이 진행되지만 큰 이득이나 진전은 없다." |

무모(Reckless)는 위와 같은 5등급이되 문장이 더 극단적이다 (`DiceDirectionText.ForReckless`):
대실패는 "돌이키기 힘든 피해", 대성공은 "판을 뒤집을 만한 큰 이득" 식. 등급 구간도 다르다
(2–3 대실패 / 4–6 실패 / 7–8 부분 / 9–10 성공 / 11–12 대성공).

`ChatController`가 앞에 `"플레이어 선택: {Choice.Text}\n"` 을 붙여 `extraHint`로 넘긴다.
화면의 등급 라벨은 `"{A} + {B} = {Total} · {ShortLabel}"` 형식이다.

## 데이터 흐름

1. `SendPlayerMessage(text)`: 입력 파싱(숫자 → 선택 / 그 외 → 자유) → 선택이면 말풍선을
   `Choice.Text`로 치환 기록 → `_awaitingResponse = true`.
2. Risky면: `DiceRoll.Roll()` → `OnDiceRollRequested` 발생 → `DiceRollOverlayView`가 주사위를
   하나씩 굴려 정착할 때마다 아래 슬롯에 찍고, 둘 다 끝나면 등급 라벨 표시 →
   `afterSettleDelay` 후 오버레이 숨김 → continuation(`ProceedWithTurn`) 호출.
   Safe/자유면 곧바로 `ProceedWithTurn`.
3. `ProceedWithTurn`: `extraHint` 조합 → `PromptBuilder.BuildTurnPrompt` →
   `provider.ContinueStory`.
4. 응답: `_awaitingResponse = false` → 나레이션 말풍선 → (NpcLine) NPC 말풍선 →
   `_lastChoices = response.Choices` → `NewFacts` 반영 → 선택지 목록 말풍선 →
   `isFinalTurn || response.IsEnding`이면 종료.

## 테스트 영향

현재 EditMode 테스트는 `HistoryWindowTests.cs`, `KeywordSetContentTests.cs` 둘뿐이었다.
Provider/Controller 테스트는 없다.

- 신규: `DiceRollTests.cs` — `Bucket` 위험/무모 각각의 경계값 + `Roll()`이 두 눈을 각각
  1~6, 합 2~12로 주는지 검증.
- 신규: `DiceDirectionTextTests.cs` — `ForRisky`/`ForReckless` 각 5개 문장이 서로 다르고
  비지 않는지, `ForReckless`가 `ForRisky`와 등급별로 다른지, `ForSafe`가 둘 다와 다른지,
  `ShortLabel` 매핑 검증.
- 신규: `ChoiceInputParserTests.cs` — `"2"`→1, `"  3 "`→2, `"문 열어"`→null,
  `"0"`/`"6"`(범위 밖, count=5)→null, 빈 문자열→null, count=0→null.
- 신규: `GeminiProviderChoicesTests.cs` — `BuildTurnRequestBody`에 `choices` 스키마와
  `안전`/`위험`/`무모`가 들어가는지, `ParseTurnResponse`가 세 태그를 파싱하는지, choices
  없으면 빈 리스트인지.
- `ChatController` / `DiceRollOverlayView`는 `DataManager.Instance`(MonoBehaviour 싱글턴) /
  씬 오브젝트에 의존하므로 완전한 단위 테스트는 어렵다. 전체 흐름(선택 → 주사위 오버레이 →
  continuation → LLM 호출 → 나레이션 반영)은 Play 모드 스모크 테스트로 확인한다. 파싱·주사위·
  문장 로직은 위의 순수 함수 테스트로 커버한다.
