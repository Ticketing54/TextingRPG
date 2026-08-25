# 스토리그래프 제거 및 누적 요약 방식 전환 설계

## 배경

`StoryGraph`/`StoryNode`/`StoryTransition`/`StoryProgression`으로 구성된 고정 노드 그래프는
게르트 NPC의 이야기를 6개 노드(start/chat/wary/room/rumor/kicked_out) 안에서만 움직이게
제한한다. 실제로 플레이해보니 이야기가 "너무 정해진 틀 안에서만" 움직이는 문제가 있었다.

또한 대화 히스토리는 `HistoryWindow.TakeRecent`로 최근 8개 메시지만 잘라서 LLM에게
보내고 있어서, 노드 그래프가 없어지면 그보다 이전에 일어난 일을 LLM이 기억할 방법이
없어진다. 이를 보완하기 위해, 노드 대신 LLM이 매 턴 직접 갱신하는 "지금까지 일어난 일
요약"을 도입한다.

## 목표

- 정해진 노드/전이 없이, LLM이 플레이어 행동에 따라 자유롭게 다음 장면을 서술하게 한다.
- 8개 메시지 창 밖으로 밀려난 과거 사건도 요약을 통해 계속 맥락에 남는다.
- 호감도/스탯(`EffectApplier`), 100턴 제한과 80턴 마무리 힌트, 키워드 인트로 등
  그래프와 무관한 기존 기능은 그대로 유지한다.

## 범위

### 제거

- `Assets/02_Script/Runtime/Story/StoryGraph.cs`
- `Assets/02_Script/Runtime/Story/StoryNode.cs`
- `Assets/02_Script/Runtime/Story/StoryTransition.cs`
- `Assets/02_Script/Runtime/Story/StoryProgression.cs`
- `Assets/05_Data/Story/GertStoryGraph.asset`
- `LLMResponse.Tags` 및 관련 프롬프트 문구(`[이번 턴에 사용 가능한 태그]`)
- `ChatController`의 `OnStoryNodeChanged` 이벤트, `currentNode`/`StoryProgression.Resolve` 로직
- `PlayerState`의 `_storyNodes` 딕셔너리 및 `GetStoryNode`/`SetStoryNode`

### 추가/변경

- `LLMResponse`에 `Summary` 필드 추가 (매 턴 LLM이 새로 쓰는 누적 요약 전체 텍스트)
- `PlayerState`에 `_summaries` 딕셔너리 + `GetSummary(npcId)`/`SetSummary(npcId, text)` 추가
  (기존 npcId 키잉 패턴과 동일)
- `GeminiProvider`의 `responseSchema`: `tags` 제거, `summary`(required) 추가
- `SystemPromptBuilder.Build`: `StoryNode currentNode` 파라미터 대신 `string summary`를
  받아 `[지금까지 일어난 일]` 섹션에 넣음. 태그 안내 문구 제거
- `SystemPromptBuilder.BuildOpening`: `StoryNode startNode` 파라미터 제거 (세계관 +
  키워드만으로 오프닝 생성)
- `ChatController.BeginAdventure`: 시작 노드 없이 오프닝 생성, 초기 요약은 빈 문자열로 시작
- `ChatController.SendPlayerMessage`: 이전 요약을 프롬프트에 넣고, 응답의 `Summary`를
  `PlayerState`에 저장. 태그 기반 노드 전이 로직 제거
- `ChatBootstrap`: `storyGraph` 필드 및 `ChatController` 생성자 인자에서 제거
- `GertContentTests.cs`: 그래프 관련 테스트(`GertStoryGraph_HasExpectedNodesAndTransitions`)
  제거, NPC 정의 테스트(`GertNpcAsset_HasExpectedFields`)는 유지

### 유지 (변경 없음)

- `EffectApplier`, `PlayerState`의 호감도/스탯 딕셔너리
- 100턴 제한(`MaxTurns`) + 80턴 마무리 힌트(`WrapUpTurnThreshold`)
- `NPCDefinition`, `KeywordIntroPanel`, `worldDescription`
- `HistoryWindow.TakeRecent` (최근 8개 메시지 컷)

## 토큰 소모 대응: 요약 길이 상한

`Summary`는 고정 텍스트였던 `SceneDescription`과 달리 매 턴 LLM이 새로 생성해서 출력하는
필드다. 길이 제한이 없으면 턴이 진행될수록 요약이 계속 길어져 입력(다음 턴 프롬프트에
재삽입)과 출력(매 턴 재생성) 토큰이 동반 증가할 수 있다. 이를 막기 위해
`SystemPromptBuilder.Build`/`BuildOpening`의 지시문에 요약 길이 상한을 명시한다:

> "요약은 항상 5문장 이내로 압축해서 다시 써라. 오래된 세부사항은 최근 상황을 이해하는 데
> 더 이상 필요하지 않으면 자연스럽게 생략해라."

이 지시문 덕분에 요약 크기는 턴 수와 무관하게 대략 일정하게 유지되고, 턴당 토큰 증가폭은
(태그 필드 제거로 상쇄되는 부분도 있어) 노드 방식 대비 소폭 증가하되 폭주하지 않는다.

## 데이터 흐름

1. `BeginAdventure()`: 요약 없이(빈 문자열) 오프닝 프롬프트 생성 → LLM이 `Narration`/
   `NpcLine`/`Summary`(초기 요약) 응답 → `PlayerState.SetSummary(npcId, response.Summary)`
2. `SendPlayerMessage(text)`: `PlayerState.GetSummary(npcId)`로 이전 요약을 읽어
   `SystemPromptBuilder.Build`에 전달 → LLM이 `Narration`/`NpcLine`/`Summary`(갱신된 요약)/
   `Effects` 응답 → 요약을 덮어써서 저장, `EffectApplier.Apply`는 기존과 동일하게 동작
3. 매 턴 시스템 프롬프트의 `[지금까지 일어난 일]` 섹션에는 항상 "가장 최근에 LLM이 쓴
   요약 전체"가 들어간다 (append가 아니라 매번 교체).

## 에러 처리

기존과 동일. `responseSchema`에 `summary`를 required로 추가하므로, Gemini가 스키마를
따르지 않으면 기존 파싱 실패 경로(`onError`)를 그대로 탄다. 별도의 새 에러 케이스는
없다.

## 테스트 영향

- 삭제: `Assets/02_Script/Tests/EditMode/StoryProgressionTests.cs`
- 수정: `PlayerStateTests`(요약 getter/setter로 교체), `SystemPromptBuilderTests`(시그니처
  변경 반영), `ChatControllerTests`(요약 저장/전달 검증으로 교체, 노드 전이 검증 제거),
  `GeminiProviderTests`(스키마 변경 반영), `GertContentTests`(그래프 테스트 제거)
