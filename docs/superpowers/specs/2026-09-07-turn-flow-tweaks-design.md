# 턴 흐름 개선 3건 설계

플레이테스트에서 나온 문제 3가지를 묶어 처리한다.

## 1. 안전 선택지 강제 완화

**문제:** `PromptBuilder`가 매 턴 "가능하면 안전한 선택지와 위험한 선택지를 섞는다"고
지시해, LLM이 습관적으로 안전 선택지를 하나씩 끼워넣는다. 플레이어가 계속 안전만 고르면
`DiceDirectionText.ForSafe()`("큰 진전 없음")가 매 턴 프롬프트에 들어가 이야기가 정체된다.

**수정:** `PromptBuilder.BuildTurnPrompt` 지시문에서
```
"가능하면 안전한 선택지와 위험한 선택지를 섞는다. "
```
를
```
"선택지는 위험·무모 위주로 구성하고, 안전한 선택지는 상황상 실제로 물러서거나 지켜볼
여지가 있을 때만 넣는다 (없어도 된다). 매 턴 습관적으로 안전한 선택지를 넣지 않는다. "
```
로 교체. `ForSafe()` 문구 자체는 그대로 둔다 (안전이 항상 존재하던 게 문제였음).

## 2. off-topic 입력 처리

**목표:** 플레이어가 이야기와 무관한 말(잡담, 메타 발언, 무의미한 문자열)을 하면 그 입력을
턴으로 치지 않고, 짧은 안내 + 직전 선택지를 다시 보여준다.

**감지:** LLM이 판단한다 (클라이언트 휴리스틱으로는 부정확). off-topic 입력도 API 호출
1회는 소비된다.

### 스키마 / 프롬프트

- `TurnResponse.IsOffTopic` (bool) 추가.
- `GeminiProvider.BuildTurnRequestBody`: `responseSchema.properties`에 `offTopic`(BOOLEAN)
  추가, `required`에 포함.
- `GeminiProvider.ParseTurnResponse`: `IsOffTopic = (bool?)payload["offTopic"] ?? false`.
- `PromptBuilder.BuildTurnPrompt` 지시문 추가:
  - "플레이어 입력이 이 이야기·세계관과 전혀 무관한 내용(잡담, 게임 밖 이야기, 메타 발언,
    무의미한 문자열 등)이면 offTopic을 true로 설정하고, narration에는 지금 상황으로
    돌아오라는 짧은 한 문장만 쓴다. npcLine·newFacts·choices는 비운다."
  - "세계관 안에서 엉뚱하거나 비합리적인 행동을 시도하는 것은 off-topic이 아니다 (정상
    진행하되 결과로 판단한다). 그 외에는 offTopic을 false로 둔다."

### ChatController

- `ProceedWithTurn` 의 `onSuccess` 맨 앞:
  ```csharp
  if (response.IsOffTopic) { HandleOffTopic(response.Narration); return; }
  ```
- `HandleOffTopic(string redirectLine)`:
  - `_awaitingResponse = false`.
  - 방금 플레이어 메시지를 히스토리에서 제거: `GetConversationHistory()` 리스트의 마지막이
    `ChatSender.Player`면 `RemoveAt(Count - 1)`. → 턴 수·다음 프롬프트 컨텍스트에서 빠진다.
    화면의 플레이어 버블은 그대로 남는다 (제거 수단 없음, 그대로 둔다).
  - `redirectLine`이 비어 있으면 `"(지금 이야기와 관련 없는 말 같다. 하던 데서 이어가자.)"`
    폴백. `ChatSender.Narration` 버블로 `OnMessageAdded` — **AppendConversationMessage 안 함**.
  - `_lastChoices.Count > 0`이면 이전 선택지를 다시 버블로 `OnMessageAdded` (Plain/Colored) —
    저장 안 함.
  - `_lastChoices`, `_conversationEnded`, 집계(§3) 전부 불변.
- off-topic은 자유 입력에서만 발생한다 (선택지 번호 입력은 항상 on-topic). 따라서 주사위를
  굴리지 않은 상태이고 별도 정리가 필요 없다.

## 3. 엔딩 시 플레이어 평가

**목표:** 이야기가 끝나면 플레이 스타일 칭호 + 수치 한 줄을 마지막에 보여준다. 코드 규칙
기반, API 호출 없음. 임계값·칭호는 코드에서 바로 튜닝 가능.

### 추가: `Systems/PlayerEvaluation.cs`

```csharp
public static class PlayerEvaluation
{
    public struct Tally   // 가변 struct — ChatController가 필드로 들고 누적
    {
        public int Safe, Risky, Reckless;
        public int CritFail, Fail, Partial, Success, CritSuccess;
        public void CountChoice(ChoiceRisk risk);   // Safe/Risky/Reckless++
        public void CountOutcome(DiceOutcome outcome); // CritFail..CritSuccess++
    }

    public static string Epithet(Tally t);   // 규칙 표는 아래
    public static string Summary(Tally t);   // 엔딩 버블에 그대로 넣을 문자열 (아래)
}
```

`Summary`는 `<align=center>…</align>`로 감싼 가운데 정렬 블록. 항목마다 한 줄씩, 총평(칭호)은
맨 아래에 `<size=160%><b>…</b></size>`로 크고 굵게:
```
━ 플레이어 평가 ━

안전 2
위험 4
무모 7

대성공 2
대실패 1

무모한 돌격자   ← 크게·굵게
```

칭호 규칙 (위에서부터 먼저 맞는 것, 코드에서 수정):
| 조건 | 칭호 |
|---|---|
| `Reckless >= Risky && Reckless > Safe` | 무모한 돌격자 |
| `Safe > Risky + Reckless` | 신중한 관찰자 |
| `CritSuccess >= 3` | 운명의 총아 |
| `CritFail >= 3` | 불운한 방랑자 |
| `Risky >= Safe && Risky >= Reckless && Risky > 0` | 계산된 승부사 |
| 그 외 | 즉흥적인 여행자 |

선택을 한 번도 안 했으면(전부 자유 입력) `Tally`가 전부 0 → "즉흥적인 여행자".

### ChatController 집계

- 필드 `private PlayerEvaluation.Tally _tally;`
- `SendPlayerMessage`: `selectedChoice != null`이면 `selectedChoice.Risk`로 Safe/Risky/Reckless++.
- 주사위 굴리는 분기: `DiceRoll.Bucket` 결과 `outcome`으로 해당 카운트++.
  (안전 선택은 주사위를 안 굴리므로 outcome 집계에 없음 — 의도된 것.)

### 엔딩 표시

`ProceedWithTurn.onSuccess`에서 `ending`이 true가 되어 `OnConversationEnded` 를 부르기 직전에,
`PlayerEvaluation.Summary(_tally)` 를 `ChatSender.Narration` 버블로 큐에 넣는다. 표시 전용
(`OnMessageAdded`만, `AppendConversationMessage` 안 함). 100턴 강제 종료·`IsEnding` 조기 종료
둘 다 동일하게 표시.

## 테스트

- 신규 `PlayerEvaluationTests.cs`: 칭호 규칙 경계값 6종, `Summary` 포맷(가운데 정렬·줄바꿈),
  카운트 누적, 빈 Tally.
- `GeminiProviderChoicesTests`: `offTopic` 스키마 포함 + 파싱(true/기본 false) 케이스 추가.
- `PromptBuilderTests`: 안전 선택지 문구가 바뀌었는지 / off-topic 지시문이 들어갔는지 가볍게.
- `ChatController` off-topic 흐름·엔딩 평가 버블은 `DataManager.Instance` 의존이라 단위
  테스트가 어렵다 — Play 모드로 확인. 순수 로직은 위 신규 테스트로 커버.

## 범위 밖 (later)

- off-topic 안내에 LLM이 아니라 고정 문구만 쓰는 옵션 (지금은 LLM이 narration에 한 문장 작성).
- 엔딩 평가의 LLM 총평 한 문단 (지금은 코드 칭호 + 수치만).
- off-topic 플레이어 버블을 화면에서 제거하는 기능.
- 자유 입력 위주 플레이를 위한 별도 칭호 세분화.
