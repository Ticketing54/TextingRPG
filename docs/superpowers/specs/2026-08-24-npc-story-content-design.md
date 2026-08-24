# 첫 NPC/StoryGraph 콘텐츠 + 나레이션 응답 분리 설계

## 개요

`ChatBootstrap`이 지금까지 `ScriptableObject.CreateInstance`로 즉석 생성해 쓰던 가짜 NPC(`mock_npc`)와 스토리("start" 노드 하나뿐, 전이 없음)를 실제 Unity 에셋으로 교체한다. 이건 `2026-07-25-llm-chat-core.md` 플랜의 마지막 남은 목표("NPC 1명 + 호감도 하나 + 작은 StoryGraph 하나로 끝까지 동작")다.

동시에, NPC 응답 방식을 "NPC가 직접 대사만 치는 것"에서 "나레이션이 매 턴 행동 결과를 서술하고, NPC 대사는 상황에 따라서만 보조적으로 곁들여지는 것"으로 바꾼다. 이를 위해 `LLMResponse`를 나레이션/NPC 대사 두 필드로 분리한다.

## 범위 밖 (이번 스펙에서 다루지 않음)

- `GenrePreset` 시스템 (장르 선택, 여러 NPC 로스터) — 마스터 스펙(`2026-07-25-messenger-llm-rpg-design.md`)의 Plan 2/3/4 영역
- `IntentTag`를 코드 레벨 enum/공용 타입으로 formalize하는 것 — 지금처럼 각 `StoryNode.Transitions`에 문자열 태그로 느슨하게 정의
- 전투, 인벤토리, 세이브/로드, 엔딩 소설화, 결제
- 대화 히스토리 요약(LLM 기반 압축) — 지금처럼 최근 N개 truncate만
- 429 재시도 백오프 (이미 클라이언트 쪽 전송 쿨다운으로 대응 중, 별도 스펙 없이 보류)

## 데이터 모델 변경

### `LLMResponse`

```csharp
public class LLMResponse
{
    public string Narration;   // 기존 Reply를 이름 변경. 매 턴 항상 채워짐 — 행동 결과를 나레이션 톤으로 서술
    public string NpcLine;     // 새 필드. NPC가 이번 턴 직접 말하지 않았으면 빈 문자열
    public List<string> Tags = new List<string>();
    public List<LLMEffect> Effects = new List<LLMEffect>();
}
```

### `GeminiProvider`

- `responseSchema`의 `reply` 필드를 `narration`(required)으로 이름 변경, `npcLine`(required 아님, 빈 문자열 허용) 필드를 새로 추가한다.
- `ParseResponse`가 `payload["narration"]` → `Narration`, `payload["npcLine"]` → `NpcLine`(없으면 빈 문자열)으로 매핑한다.
- 그 외 요청/응답 처리(재시도, 파싱 실패 처리)는 변경 없음.

### `SystemPromptBuilder`

마지막 지시문을 아래 방향으로 수정한다 (정확한 문구는 구현 시 다듬는다):

> "너는 이 상황을 3인칭 나레이션으로 서술한다. 플레이어 행동의 결과를 묘사하고, 그 안에서 캐릭터가 실제로 입을 열어 말할 만한 상황이면 그 대사를 `npcLine`에 짧게 담아라. 캐릭터가 이번 턴에 말할 필요가 없으면 `npcLine`은 빈 문자열로 둔다."

## `ChatController` 배선 변경

`SendPlayerMessage`의 `onSuccess` 콜백에서, 지금은 `ChatMessage(Npc, response.Reply)` 하나만 만드는데 이걸 아래로 바꾼다:

```csharp
var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, timestamp);
_playerState.AppendMessage(_npc.NpcId, narrationMessage);
OnMessageAdded?.Invoke(narrationMessage);

if (!string.IsNullOrEmpty(response.NpcLine))
{
    var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, timestamp);
    _playerState.AppendMessage(_npc.NpcId, npcMessage);
    OnMessageAdded?.Invoke(npcMessage);
}
```

- 나레이션 메시지가 항상 먼저, NPC 대사는 있을 때만 그 뒤에 추가로 발행된다. `ChatBubbleListView`는 이미 큐 기반으로 순서대로 재생하므로 별도 변경이 필요 없다 — 나레이션 버블(가운데 정렬) 다음에 NPC 버블(왼쪽 정렬 + 이름표)이 순서대로 타이핑된다.
- `tags`/`effects` 처리, `StoryProgression.Resolve` 호출은 변경 없음.
- **검증된 사실**: Gemini API에 `contents` 배열의 `role`이 연속으로 `model`, `model`로 와도(나레이션 메시지 다음 턴에 또 나레이션이 오는 식으로 히스토리에 쌓여도) API가 정상 응답한다(`code=200`으로 실제 호출해 확인함). 두 메시지를 히스토리에 별도로 저장해도 다음 턴 API 호출이 깨지지 않는다.

## NPC/스토리 콘텐츠

### NPCDefinition — 게르트

| 필드 | 값 |
|---|---|
| `NpcId` | `innkeeper_gert` |
| `DisplayName` | 게르트 |
| `PersonaDescription` | 다정하지만 무뚝뚝한 말투의 초로의 여관 주인. 오랫동안 온갖 여행자를 상대해 사람 보는 눈이 좋다. 마을 소문에 밝고, 무례한 손님에게는 단호하다. |

### 세계관 (`ChatBootstrap.worldDescription`)

> 중세 판타지 세계, 변방의 작은 마을. 플레이어는 이제 막 마을에 도착한 여행자다.

### StoryGraph — `innkeeper_gert`, 시작 노드 `start`

```
        friendly
 start ─────────→ chat ──ask_room──→ room (끝)
   │                │
   │hostile         └──ask_rumor──→ rumor (끝)
   ↓
 wary ──apologize──→ (chat로 복귀)
   │
   └──threaten──→ kicked_out (끝)
```

| 노드 | `SceneDescription` (시스템 프롬프트용, 플레이어에게 비표시) | 전이 (태그 → 다음 노드) |
|---|---|---|
| `start` | 플레이어가 방금 여관 문을 열고 들어왔다. 늦은 저녁이라 손님은 거의 없고, 벽난로 앞에 게르트가 손님을 맞이할 준비를 하고 있다. | `friendly`→`chat`, `hostile`→`wary` |
| `chat` | 플레이어와 게르트가 편하게 대화를 나누고 있다. 게르트는 여행자들의 이야기를 듣는 걸 좋아한다. | `ask_room`→`room`, `ask_rumor`→`rumor` |
| `wary` | 플레이어가 게르트에게 무례하거나 위협적으로 굴어서, 게르트가 경계하며 거리를 두고 있다. | `apologize`→`chat`, `threaten`→`kicked_out` |
| `room` | 플레이어가 방을 잡기로 했다. 게르트가 열쇠를 건네주며 안내한다. | (없음 — 끝 노드) |
| `rumor` | 게르트가 마을에 떠도는 소문 하나를 들려주고 있다. | (없음 — 끝 노드) |
| `kicked_out` | 게르트가 화가 나서 플레이어를 여관 밖으로 쫓아냈다. | (없음 — 끝 노드) |

끝 노드는 나가는 전이가 없을 뿐, 매칭되는 태그가 없으면 `StoryProgression.Resolve`가 같은 노드를 유지하므로 그 안에서 잡담은 계속 이어갈 수 있다. 별도의 "대화 종료" 처리는 필요 없다.

사용되는 태그 어휘(6개, 이번 StoryGraph 한정): `friendly`, `hostile`, `ask_room`, `ask_rumor`, `apologize`, `threaten`.

## 에셋 생성 & `ChatBootstrap` 연결

프로젝트 폴더 번호 규칙(`01_Scene`, `02_Script`, `03_Resource`, `04_Prefab`)에 맞춰 새 폴더를 만든다:

- `Assets/05_Data/NPC/Gert.asset` (`NPCDefinition`)
- `Assets/05_Data/Story/GertStoryGraph.asset` (`StoryGraph`)

`ChatBootstrap.cs`는 `Start()`에서 `ScriptableObject.CreateInstance`로 NPC/StoryGraph를 즉석 생성하던 코드를 제거하고, 대신 인스펙터에서 할당하는 필드로 바꾼다:

```csharp
[SerializeField] NPCDefinition npc;
[SerializeField] StoryGraph storyGraph;
[SerializeField] string worldDescription = "중세 판타지 세계, 변방의 작은 마을. 플레이어는 이제 막 마을에 도착한 여행자다.";
```

씬의 `ChatBootstrap` 컴포넌트에 위 두 에셋을 끌어다 끼운다.

## 테스트 영향

`Reply` → `Narration` 이름 변경 때문에 아래 기존 테스트들이 컴파일 실패한다 — 기계적으로 필드명만 맞춰 고친다 (동작 변경 없음):

- `ChatControllerTests.cs`
- `GeminiProviderTests.cs`
- `MockLLMProviderTests.cs`

`ChatController`가 이제 성공 시 메시지를 1개 또는 2개 발행할 수 있으므로, `ChatControllerTests`에 "NpcLine이 비어있으면 메시지 1개만 발행", "NpcLine이 있으면 나레이션+NPC 순서로 2개 발행"을 검증하는 케이스를 추가한다.

## 수동 검증 (End-to-End)

1. Play 모드에서 게르트에게 인사 → `start`에서 `friendly` 태그로 `chat`으로 전이되는지 확인
2. 방을 잡고 싶다고 요청 → `room`으로 전이, 나레이션 버블 + (있다면) NPC 버블이 순서대로 표시되는지 확인
3. 무례하게 말하기 → `wary`로 전이 확인
4. `wary` 상태에서 계속 위협 → `kicked_out`으로 전이 확인
5. 각 케이스에서 나레이션 버블(가운데 정렬)과 NPC 버블(왼쪽 정렬 + "게르트" 이름표)이 구분되어 보이는지 육안 확인
