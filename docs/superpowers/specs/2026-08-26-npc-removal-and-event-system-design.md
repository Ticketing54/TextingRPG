# NPC 정의 제거 및 확률 기반 이벤트/엔딩 판별 시스템 설계

## 배경

지금까지는 `NPCDefinition` ScriptableObject(`Gert.asset`)로 대화 상대(여관 주인 게르트)를
미리 만들어 두고, 게임 내내 그 캐릭터 하나와만 대화하는 구조였다. 이걸 없애고 대화 상대를
포함한 등장인물 전체를 AI가 즉흥으로 만들어내게 바꾼다.

동시에, 매 플레이어 턴마다 확률적으로 "이번 턴에 특별한 사건이 일어날 수도 있다"는 후보를
뽑아 LLM에게 제안하는 이벤트 시스템을 추가한다. LLM은 문맥상 자연스러우면 반영하고, 부자연스
러우면 무시할 수 있다. 이 중 "즉사"처럼 이야기를 끝낼 수도 있는 사건이 있는데, 이야기가
끝났는지 여부는 이벤트 종류와 무관하게 **매 턴 범용적으로 판별**한다 — 100턴을 다 채우지 않고
도 자연스럽게 끝날 수 있다. 100턴은 그 이전에 안 끝났을 때의 최후 안전장치로만 남는다.

## 목표

- 대화 상대가 고정 캐릭터 하나에 묶이지 않고, 필요할 때(조력자 등장 등) AI가 즉석에서 인물을
  만들어낼 수 있게 한다.
- 매 턴 확률적으로 좋은 일/나쁜 일/조력자 등장/즉사 후보를 제시하고, 반영 여부는 AI가 문맥에
  맞게 판단하게 한다. 확률 값은 ScriptableObject로 관리해 코드 수정 없이 조율 가능하게 한다.
- 이야기가 100턴을 다 채우지 않아도, 매 턴 LLM이 "지금 끝나는 게 자연스러운가"를 범용적으로
  판단해서 조기 종료할 수 있게 한다.

## 범위

### 제거

- `Assets/02_Script/Runtime/NPC/NPCDefinition.cs`
- `Assets/05_Data/NPC/Gert.asset` 및 `Assets/05_Data/NPC/` 폴더
- `Assets/02_Script/Tests/EditMode/GertContentTests.cs`
- `ChatBootstrap.npc` 필드, `ChatController`의 `NPCDefinition`/`NpcDisplayName` 관련 코드
- `PlayerState`/`EffectApplier`의 npcId 매개변수 (세션이 하나뿐이라 더 이상 구분 필요 없음)
- NPC 말풍선 이름표 표시 (고정 캐릭터가 없으므로 당분간 비워둠 — `ChatBubble.cs` 자체는
  안 건드리고, 호출부에서 senderName을 안 넘기는 것만으로 자연히 숨겨짐)

### 추가

- `Assets/02_Script/Runtime/Core/EventCategory.cs` — `None`/`Good`/`Bad`/`AllyAppears`/`Death`
  열거형
- `Assets/02_Script/Runtime/Core/EventChanceConfig.cs` — 카테고리별 가중치를 담는
  ScriptableObject
- `Assets/02_Script/Runtime/Core/EventRoller.cs` — 가중치 기반 랜덤 뽑기 (테스트용으로 0~1
  롤 값을 직접 주입하는 오버로드 포함)
- `Assets/02_Script/Runtime/Core/EventHintText.cs` — `EventCategory` → 프롬프트에 넣을 힌트
  문자열 매핑 (`None`은 빈 문자열)
- `Assets/05_Data/Event/DefaultEventChanceConfig.asset` — 기본 가중치 데이터
- `LLMResponse.IsEnding`(bool) — 매 턴 LLM이 이야기가 끝났는지 보고하는 범용 플래그

### 변경

- `PlayerState`: `_conversationHistories`/`_summaries`/`_turnCounts` 딕셔너리를 단일 필드로
  단순화 (`List<ChatMessage> _history`, `string _summary`, `int _turnCount`). `_relationships`
  딕셔너리는 유지하되 키를 "npcId"가 아니라 "효과가 적용되는 캐릭터 이름"으로 재해석한다
  (구조는 그대로, 의미만 바뀜).
- `EffectApplier.Apply(PlayerState, IEnumerable<LLMEffect>)`: npcId 매개변수 제거.
  `relationship` 타입 효과는 `effect.Target`(캐릭터 이름)을 키로 사용한다 (기존엔 고정
  npcId를 썼고 `Target`은 무시됐음).
- `GeminiProvider`: `responseSchema`에 `isEnding`(BOOLEAN, required) 추가. `ParseResponse`가
  이 필드를 `LLMResponse.IsEnding`으로 매핑.
- `SystemPromptBuilder.Build(string worldDescription, string summary, string extraHint = "")`:
  `NPCDefinition npc`/`StoryNode` 관련 매개변수 없음(이미 없음). 캐릭터 고정 섹션
  (`[캐릭터: ...]`) 제거. `extraHint`는 기존 `endingHint`를 일반화한 이름으로, 마무리
  힌트와 이벤트 힌트를 이어붙여 전달한다. 지시문에 다음을 추가:
  - "매 턴, 이번 턴에 일어난 일까지 포함해 이야기가 여기서 끝나는 게 자연스러우면 isEnding을
    true로 보고해라 (사망, 만족스러운 결말 등 이유는 다양할 수 있다). 계속 이어가는 게
    자연스러우면 false로 둔다."
  - "relationship 효과를 보고할 때 target에는 호감도가 바뀌는 대상 캐릭터의 이름을 적어라."
- `SystemPromptBuilder.BuildOpening(string worldDescription)`: 캐릭터 고정 섹션 제거 외
  변경 없음 (오프닝에는 이벤트 시스템을 적용하지 않는다 — 플레이어가 아직 아무 행동도 하지
  않았으므로).
- `ChatController`:
  - 생성자에서 `NPCDefinition` 제거, `EventChanceConfig`를 새로 받는다:
    `ChatController(PlayerState, ILLMProvider, string worldDescription, EventChanceConfig)`.
  - `NpcDisplayName` 프로퍼티 제거.
  - `SendPlayerMessage`에서 매 턴 `EventRoller.Roll(_eventConfig)`로 카테고리를 뽑고,
    `EventHintText.For(category)`와 기존 마무리 힌트(80/100턴)를 합쳐 `extraHint`로 전달.
  - 응답 처리 시 `isFinalTurn(턴수 100) || response.IsEnding` 이면 대화를 종료한다 (기존
    100턴 강제 종료는 안전장치로 유지, `IsEnding`으로 조기 종료 가능).
  - `PlayerState`/`EffectApplier` 호출에서 npcId 인자 제거.
- `ChatBootstrap`: `npc` 필드 제거, `eventChanceConfig` 필드 추가, `ChatController` 생성자
  호출부 갱신.
- `ChatBubbleListView.TryPlayNext`: `_current.Play(message.Sender, message.Text,
  _controller.NpcDisplayName)` → `_current.Play(message.Sender, message.Text)` (senderName
  인자 생략, `ChatBubble.Play`의 기본값 `null`을 사용 — `ChatBubble.cs` 자체는 무수정).

### 이번 범위에 포함하지 않음 (later)

- 엔딩 나레이션을 보여준 뒤 플레이어가 버튼을 눌러 Intro 씬으로 돌아가는 흐름. 지금은
  `IsEnding`(또는 100턴 도달)이 true가 되면 기존과 동일하게 입력창/전송 버튼을 잠그는 것으로
  끝난다. `OnConversationEnded` 이벤트는 그대로 유지되므로, 나중에 이 이벤트를 받아 Intro로
  전환하는 로직을 얹기만 하면 된다.
- 즉흥으로 등장한 캐릭터의 이름을 채팅 말풍선에 표시하는 기능 (이번엔 이름표 없이 진행).

## 이벤트 확률 및 힌트 텍스트

`EventChanceConfig` 필드(기본값, 상대적 가중치 — 합이 100일 필요 없음):

| 카테고리 | 기본 가중치 | 의미 |
|---|---|---|
| None | 70 | 평온, 아무 특별한 일도 일어나지 않음 (힌트 없음) |
| Good | 10 | 뜻밖의 좋은 일이 일어날 후보 |
| Bad | 10 | 좋지 않은 일이 일어날 후보 |
| AllyAppears | 5 | 플레이어를 도와줄 새 인물이 등장할 후보 |
| Death | 5 | 플레이어가 즉사할 수도 있는 결정적 위험이 닥칠 후보 |

`EventRoller.Roll(config, roll01)`은 0~1 사이 값을 받아 누적 가중치 구간에 따라 카테고리를
결정한다 (`Roll(config)`는 내부적으로 `UnityEngine.Random.value`를 사용하는 실제 게임용
오버로드). 모든 카테고리는 "반영해도 되고, 문맥상 부자연스러우면 무시해도 된다"는 뉘앙스를
힌트에 명시한다 — 강제가 아니라 후보 제안이다. `Death` 힌트에는 추가로 "정말 반영한다면
isEnding을 true로 보고하라"는 문구가 들어간다.

## 데이터 흐름

1. `SendPlayerMessage(text)`: 플레이어 메시지 기록 → 턴수 증가 → 마무리 힌트(80/100턴) 계산
   → `EventRoller.Roll(_eventConfig)`로 이번 턴 이벤트 후보 계산 → `EventHintText.For(...)`로
   변환 → 마무리 힌트와 합쳐 `extraHint` 구성 → `SystemPromptBuilder.Build(worldDescription,
   summary, extraHint)`로 프롬프트 생성 → LLM 호출.
2. 응답 수신: 나레이션/NPC 대사 기록 → 요약 갱신 → `EffectApplier.Apply(state, effects)` →
   `isFinalTurn(100턴 도달) || response.IsEnding`이면 대화 종료 처리 (기존
   `OnConversationEnded` 경로 그대로).

## 테스트 영향

- 삭제: `Assets/02_Script/Tests/EditMode/GertContentTests.cs`
- 신규: `EventRollerTests.cs`(가중치 경계값 검증), `EventHintTextTests.cs`(카테고리별 힌트
  문자열 검증)
- 수정: `PlayerStateTests.cs`(npcId 없는 API로), `EffectApplierTests.cs`(npcId 매개변수 제거,
  기존 단언은 `Target`이 이미 `"npc_a"`라서 그대로 유지 가능), `GeminiProviderTests.cs`
  (`isEnding` 스키마/파싱 검증 추가), `SystemPromptBuilderTests.cs`(NPC 관련 설정 제거,
  `isEnding`/`relationship target` 지시문 검증 추가), `ChatControllerTests.cs`(NPCDefinition
  제거, `EventChanceConfig` 주입 — 가중치를 한 카테고리에 몰아서 결정론적으로 테스트,
  `IsEnding`으로 100턴 이전 조기 종료되는 케이스 추가)
