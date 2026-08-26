# NPC 정의 제거 및 확률 기반 이벤트/엔딩 판별 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 고정 `NPCDefinition` 캐릭터를 없애고 등장인물을 완전히 즉흥적으로 만들되, 매 플레이어
턴마다 확률 기반 이벤트 후보(평온/좋은 일/나쁜 일/조력자 등장/즉사)를 제시하고, 이야기가
끝났는지는 이벤트 종류와 무관하게 매 턴 범용적으로(`IsEnding`) 판별한다.

**Architecture:** 새 `EventChanceConfig`(ScriptableObject) + `EventRoller` + `EventHintText`가
매 턴 하나의 이벤트 후보를 뽑아 프롬프트 힌트로 변환한다. `LLMResponse.IsEnding`을 매 턴
LLM이 보고하고, `ChatController`는 100턴 도달 또는 `IsEnding`이 true면 대화를 종료한다.
`PlayerState`/`EffectApplier`는 세션이 하나뿐이라는 전제로 npcId 키잉을 제거하고, 관계 효과는
`LLMEffect.Target`(캐릭터 이름)으로 키잉한다.

**Tech Stack:** Unity 6000.3.10f1, C#, Unity Test Framework (NUnit, EditMode)

## Global Constraints

- 이벤트 시스템의 가중치는 `EventChanceConfig` ScriptableObject로 관리하고, 코드에 하드코딩하지
  않는다.
- 100턴 강제 종료(`MaxTurns`=100), 80턴 마무리 힌트(`WrapUpTurnThreshold`=80), 최근 8개 메시지
  히스토리 창(`MaxHistoryMessages`=8)은 안전장치로 그대로 유지한다.
- 엔딩 나레이션 확인 버튼 + Intro 씬 복귀는 이번 범위 밖 (나중 작업). 지금은 종료 시 기존과
  동일하게 입력창/전송 버튼을 잠그기만 한다.
- 각 태스크 종료 시 Unity EditMode 테스트 전체가 통과해야 한다 (`mcp__UnityMCP__run_tests`
  사용, 실행 전 `mcp__UnityMCP__refresh_unity(wait_for_ready: true, compile: request)`로 컴파일
  갱신 후 `read_console`로 컴파일 에러 없는지 확인).

---

### Task 1: 확률 기반 이벤트 시스템 (독립 추가)

기존 코드를 건드리지 않는 순수 추가 작업이라 가장 먼저, 안전하게 진행한다.

**Files:**
- Create: `Assets/02_Script/Runtime/Core/EventCategory.cs`
- Create: `Assets/02_Script/Runtime/Core/EventChanceConfig.cs`
- Create: `Assets/02_Script/Runtime/Core/EventRoller.cs`
- Create: `Assets/02_Script/Runtime/Core/EventHintText.cs`
- Test: `Assets/02_Script/Tests/EditMode/EventRollerTests.cs`
- Test: `Assets/02_Script/Tests/EditMode/EventHintTextTests.cs`

**Interfaces:**
- Produces: `EventCategory` enum (`None`, `Good`, `Bad`, `AllyAppears`, `Death`)
- Produces: `EventChanceConfig.NoneWeight/GoodWeight/BadWeight/AllyAppearsWeight/DeathWeight : float`
- Produces: `EventRoller.Roll(EventChanceConfig config, float roll01) : EventCategory`,
  `EventRoller.Roll(EventChanceConfig config) : EventCategory`
- Produces: `EventHintText.For(EventCategory category) : string` (빈 문자열 for `None`)

- [ ] **Step 1: `EventCategory.cs` 작성**

```csharp
namespace TextingRPG.Core
{
    public enum EventCategory
    {
        None,
        Good,
        Bad,
        AllyAppears,
        Death
    }
}
```

- [ ] **Step 2: `EventChanceConfig.cs` 작성**

```csharp
using UnityEngine;

namespace TextingRPG.Core
{
    [CreateAssetMenu(fileName = "NewEventChanceConfig", menuName = "TextingRPG/Event Chance Config")]
    public class EventChanceConfig : ScriptableObject
    {
        public float NoneWeight = 70f;
        public float GoodWeight = 10f;
        public float BadWeight = 10f;
        public float AllyAppearsWeight = 5f;
        public float DeathWeight = 5f;
    }
}
```

- [ ] **Step 3: `EventRoller.cs` 작성**

```csharp
using UnityEngine;

namespace TextingRPG.Core
{
    public static class EventRoller
    {
        public static EventCategory Roll(EventChanceConfig config, float roll01)
        {
            float total = config.NoneWeight + config.GoodWeight + config.BadWeight
                + config.AllyAppearsWeight + config.DeathWeight;
            float scaled = roll01 * total;

            float cumulative = config.NoneWeight;
            if (scaled < cumulative) return EventCategory.None;

            cumulative += config.GoodWeight;
            if (scaled < cumulative) return EventCategory.Good;

            cumulative += config.BadWeight;
            if (scaled < cumulative) return EventCategory.Bad;

            cumulative += config.AllyAppearsWeight;
            if (scaled < cumulative) return EventCategory.AllyAppears;

            return EventCategory.Death;
        }

        public static EventCategory Roll(EventChanceConfig config) => Roll(config, Random.value);
    }
}
```

- [ ] **Step 4: `EventHintText.cs` 작성**

```csharp
namespace TextingRPG.Core
{
    public static class EventHintText
    {
        public static string For(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Good:
                    return "이번 턴, 플레이어에게 뜻밖의 좋은 일이 일어날 후보다. 지금까지 " +
                        "맥락상 자연스럽다면 긍정적인 사건을 반영해라. 부자연스러우면 무시해라.";

                case EventCategory.Bad:
                    return "이번 턴, 플레이어에게 좋지 않은 일이 일어날 후보다. 지금까지 맥락상 " +
                        "자연스럽다면 반영해라. 너무 가혹하거나 부자연스러우면 무시해라.";

                case EventCategory.AllyAppears:
                    return "이번 턴, 플레이어를 도와줄 새로운 인물이 등장할 후보다. 지금까지 " +
                        "맥락상 자연스럽다면 새로운 조력자를 등장시켜라. 부자연스러우면 무시해라.";

                case EventCategory.Death:
                    return "이번 턴, 플레이어가 즉사할 수도 있는 결정적 위험이 닥칠 후보다. " +
                        "지금까지 맥락상 정말 자연스럽다면 플레이어가 사망하는 것으로 반영하고 " +
                        "isEnding을 true로 보고해라. 조금이라도 부자연스럽거나 뜬금없다면 " +
                        "무시하고 평범하게 진행해라.";

                default:
                    return "";
            }
        }
    }
}
```

- [ ] **Step 5: `EventRollerTests.cs` 작성**

```csharp
using NUnit.Framework;
using TextingRPG.Core;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class EventRollerTests
    {
        private static EventChanceConfig MakeConfig()
        {
            var config = ScriptableObject.CreateInstance<EventChanceConfig>();
            config.NoneWeight = 70f;
            config.GoodWeight = 10f;
            config.BadWeight = 10f;
            config.AllyAppearsWeight = 5f;
            config.DeathWeight = 5f;
            return config;
        }

        [Test]
        public void Roll_LowValue_ReturnsNone()
        {
            Assert.AreEqual(EventCategory.None, EventRoller.Roll(MakeConfig(), 0f));
        }

        [Test]
        public void Roll_JustBelowNoneBoundary_ReturnsNone()
        {
            Assert.AreEqual(EventCategory.None, EventRoller.Roll(MakeConfig(), 0.699f));
        }

        [Test]
        public void Roll_AtNoneBoundary_ReturnsGood()
        {
            Assert.AreEqual(EventCategory.Good, EventRoller.Roll(MakeConfig(), 0.70f));
        }

        [Test]
        public void Roll_JustBelowGoodBoundary_ReturnsGood()
        {
            Assert.AreEqual(EventCategory.Good, EventRoller.Roll(MakeConfig(), 0.79f));
        }

        [Test]
        public void Roll_AtGoodBoundary_ReturnsBad()
        {
            Assert.AreEqual(EventCategory.Bad, EventRoller.Roll(MakeConfig(), 0.80f));
        }

        [Test]
        public void Roll_AtBadBoundary_ReturnsAllyAppears()
        {
            Assert.AreEqual(EventCategory.AllyAppears, EventRoller.Roll(MakeConfig(), 0.90f));
        }

        [Test]
        public void Roll_AtAllyAppearsBoundary_ReturnsDeath()
        {
            Assert.AreEqual(EventCategory.Death, EventRoller.Roll(MakeConfig(), 0.95f));
        }

        [Test]
        public void Roll_HighValue_ReturnsDeath()
        {
            Assert.AreEqual(EventCategory.Death, EventRoller.Roll(MakeConfig(), 0.999f));
        }

        [Test]
        public void Roll_WithoutExplicitRollValue_ReturnsValidCategory()
        {
            var category = EventRoller.Roll(MakeConfig());
            Assert.IsTrue(System.Enum.IsDefined(typeof(EventCategory), category));
        }
    }
}
```

- [ ] **Step 6: `EventHintTextTests.cs` 작성**

```csharp
using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class EventHintTextTests
    {
        [Test]
        public void For_None_ReturnsEmptyString()
        {
            Assert.AreEqual("", EventHintText.For(EventCategory.None));
        }

        [Test]
        public void For_Good_MentionsPositiveEvent()
        {
            StringAssert.Contains("좋은 일", EventHintText.For(EventCategory.Good));
        }

        [Test]
        public void For_Bad_MentionsNegativeEvent()
        {
            StringAssert.Contains("좋지 않은 일", EventHintText.For(EventCategory.Bad));
        }

        [Test]
        public void For_AllyAppears_MentionsNewAlly()
        {
            StringAssert.Contains("도와줄 새로운 인물", EventHintText.For(EventCategory.AllyAppears));
        }

        [Test]
        public void For_Death_MentionsIsEndingFlag()
        {
            StringAssert.Contains("isEnding", EventHintText.For(EventCategory.Death));
        }

        [Test]
        public void For_AllNonNoneCategories_MentionIgnoringIfUnnatural()
        {
            StringAssert.Contains("무시", EventHintText.For(EventCategory.Good));
            StringAssert.Contains("무시", EventHintText.For(EventCategory.Bad));
            StringAssert.Contains("무시", EventHintText.For(EventCategory.AllyAppears));
        }
    }
}
```

- [ ] **Step 7: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러가 없는지 확인한다**

- [ ] **Step 8: `mcp__UnityMCP__run_tests`로 EditMode 테스트를 실행하고 전부 통과하는지 확인한다**

- [ ] **Step 9: Commit**

```bash
git add Assets/02_Script/Runtime/Core/EventCategory.cs Assets/02_Script/Runtime/Core/EventChanceConfig.cs Assets/02_Script/Runtime/Core/EventRoller.cs Assets/02_Script/Runtime/Core/EventHintText.cs Assets/02_Script/Tests/EditMode/EventRollerTests.cs Assets/02_Script/Tests/EditMode/EventHintTextTests.cs
git commit -m "feat: add weighted random event system (category, config, roller, hint text)"
```

---

### Task 2: `LLMResponse.IsEnding` 추가 (독립 추가)

`LLMResponse`에 필드를 추가하고 `GeminiProvider`가 파싱하게 하는 것만으로, 아직 아무도
`IsEnding`을 소비하지 않으므로 안전하게 독립 진행 가능하다.

**Files:**
- Modify: `Assets/02_Script/Runtime/LLM/LLMResponse.cs`
- Modify: `Assets/02_Script/Runtime/LLM/GeminiProvider.cs`
- Test: `Assets/02_Script/Tests/EditMode/GeminiProviderTests.cs`

**Interfaces:**
- Produces: `LLMResponse.IsEnding : bool`

- [ ] **Step 1: `GeminiProviderTests.cs`를 아래로 교체 (RED 확인용)**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using TextingRPG.LLM;

namespace TextingRPG.Tests
{
    public class GeminiProviderTests
    {
        private const string SampleGeminiResponse = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""role"": ""model"",
                        ""parts"": [
                            { ""text"": ""{\""narration\"":\""게르트가 손님을 맞이한다.\"",\""npcLine\"":\""어서오세요, 손님!\"",\""summary\"":\""플레이어가 여관에 들어와 인사를 나눴다.\"",\""isEnding\"":false,\""effects\"":[{\""type\"":\""relationship\"",\""target\"":\""npc_a\"",\""delta\"":1.0}]}"" }
                        ]
                    }
                }
            ]
        }";

        private const string SampleGeminiResponseWithoutNpcLine = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""role"": ""model"",
                        ""parts"": [
                            { ""text"": ""{\""narration\"":\""문이 닫힌다.\"",\""npcLine\"":\""\"",\""summary\"":\""\"",\""isEnding\"":false,\""effects\"":[]}"" }
                        ]
                    }
                }
            ]
        }";

        private const string SampleGeminiResponseIsEndingTrue = @"{
            ""candidates"": [
                {
                    ""content"": {
                        ""role"": ""model"",
                        ""parts"": [
                            { ""text"": ""{\""narration\"":\""여행자가 쓰러진다.\"",\""npcLine\"":\""\"",\""summary\"":\""여행자가 사망했다.\"",\""isEnding\"":true,\""effects\"":[]}"" }
                        ]
                    }
                }
            ]
        }";

        [Test]
        public void ParseResponse_ExtractsNarrationNpcLineSummaryIsEndingAndEffectsFromNestedJsonText()
        {
            var response = GeminiProvider.ParseResponse(SampleGeminiResponse);

            Assert.AreEqual("게르트가 손님을 맞이한다.", response.Narration);
            Assert.AreEqual("어서오세요, 손님!", response.NpcLine);
            Assert.AreEqual("플레이어가 여관에 들어와 인사를 나눴다.", response.Summary);
            Assert.IsFalse(response.IsEnding);
            Assert.AreEqual(1, response.Effects.Count);
            Assert.AreEqual("relationship", response.Effects[0].Type);
            Assert.AreEqual("npc_a", response.Effects[0].Target);
            Assert.AreEqual(1.0f, response.Effects[0].Delta);
        }

        [Test]
        public void ParseResponse_EmptyNpcLineAndSummary_ReturnsEmptyStrings()
        {
            var response = GeminiProvider.ParseResponse(SampleGeminiResponseWithoutNpcLine);

            Assert.AreEqual("문이 닫힌다.", response.Narration);
            Assert.AreEqual("", response.NpcLine);
            Assert.AreEqual("", response.Summary);
        }

        [Test]
        public void ParseResponse_IsEndingTrue_ReturnsTrue()
        {
            var response = GeminiProvider.ParseResponse(SampleGeminiResponseIsEndingTrue);

            Assert.IsTrue(response.IsEnding);
        }

        [Test]
        public void ParseResponse_NoCandidates_Throws()
        {
            const string empty = @"{ ""candidates"": [] }";

            Assert.Throws<System.Exception>(() => GeminiProvider.ParseResponse(empty));
        }

        [Test]
        public void BuildRequestBody_IncludesSystemInstructionContentsAndJsonSchema()
        {
            var provider = new GeminiProvider("fake-key", "gemini-test-model");
            var context = new ConversationContext
            {
                SystemPrompt = "너는 친절한 상인이다",
                History = new List<ChatMessage>
                {
                    new ChatMessage(ChatSender.Player, "안녕하세요", "t1"),
                    new ChatMessage(ChatSender.Npc, "어서오세요", "t2")
                }
            };

            var bodyJson = provider.BuildRequestBody(context);
            var body = JObject.Parse(bodyJson);

            Assert.AreEqual("너는 친절한 상인이다", (string)body["systemInstruction"]["parts"][0]["text"]);
            Assert.AreEqual("application/json", (string)body["generationConfig"]["responseMimeType"]);

            var contents = (JArray)body["contents"];
            Assert.AreEqual(2, contents.Count);
            Assert.AreEqual("user", (string)contents[0]["role"]);
            Assert.AreEqual("model", (string)contents[1]["role"]);

            var schemaProperties = (JObject)body["generationConfig"]["responseSchema"]["properties"];
            Assert.IsTrue(schemaProperties.ContainsKey("narration"));
            Assert.IsTrue(schemaProperties.ContainsKey("npcLine"));
            Assert.IsTrue(schemaProperties.ContainsKey("summary"));
            Assert.IsTrue(schemaProperties.ContainsKey("isEnding"));

            var requiredFields = ((JArray)body["generationConfig"]["responseSchema"]["required"])
                .Select(t => (string)t)
                .ToList();
            CollectionAssert.Contains(requiredFields, "summary");
            CollectionAssert.Contains(requiredFields, "narration");
            CollectionAssert.Contains(requiredFields, "isEnding");
            CollectionAssert.DoesNotContain(requiredFields, "npcLine");
        }

        [Test]
        public void BuildRequestBody_MapsNarrationSenderToModelRole()
        {
            var provider = new GeminiProvider("fake-key", "gemini-test-model");
            var context = new ConversationContext
            {
                SystemPrompt = "시스템",
                History = new List<ChatMessage>
                {
                    new ChatMessage(ChatSender.Narration, "밤이 깊어간다", "t1")
                }
            };

            var bodyJson = provider.BuildRequestBody(context);
            var body = JObject.Parse(bodyJson);
            var contents = (JArray)body["contents"];

            Assert.AreEqual("model", (string)contents[0]["role"]);
        }
    }
}
```

- [ ] **Step 2: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러를 확인한다 (RED)**

- [ ] **Step 3: `LLMResponse.cs`를 아래로 교체**

```csharp
using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class LLMResponse
    {
        public string Narration;
        public string NpcLine = "";
        public string Summary = "";
        public bool IsEnding;
        public List<LLMEffect> Effects = new List<LLMEffect>();
    }
}
```

- [ ] **Step 4: `GeminiProvider.cs`의 `responseSchema`와 `ParseResponse`를 수정한다**

`BuildRequestBody` 안의 `responseSchema` 블록을 아래로 교체:

```csharp
            var responseSchema = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["narration"] = new JObject { ["type"] = "STRING" },
                    ["npcLine"] = new JObject { ["type"] = "STRING" },
                    ["summary"] = new JObject { ["type"] = "STRING" },
                    ["isEnding"] = new JObject { ["type"] = "BOOLEAN" },
                    ["effects"] = new JObject { ["type"] = "ARRAY", ["items"] = effectSchema }
                },
                ["required"] = new JArray("narration", "summary", "isEnding", "effects")
            };
```

`ParseResponse`의 `payload`에서 만드는 `response` 초기화 블록을 아래로 교체:

```csharp
            var payload = JObject.Parse(text);
            var response = new LLMResponse
            {
                Narration = (string)payload["narration"],
                NpcLine = (string)payload["npcLine"] ?? "",
                Summary = (string)payload["summary"] ?? "",
                IsEnding = (bool?)payload["isEnding"] ?? false,
                Effects = new List<LLMEffect>()
            };
```

- [ ] **Step 5: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러가 없는지 확인한다**

- [ ] **Step 6: `mcp__UnityMCP__run_tests`로 EditMode 테스트를 실행하고 전부 통과하는지 확인한다**

- [ ] **Step 7: Commit**

```bash
git add Assets/02_Script/Runtime/LLM/LLMResponse.cs Assets/02_Script/Runtime/LLM/GeminiProvider.cs Assets/02_Script/Tests/EditMode/GeminiProviderTests.cs
git commit -m "feat: add general-purpose IsEnding flag to LLM response"
```

---

### Task 3: NPC 제거 + 이벤트/엔딩 판별을 코어 로직에 통합

`PlayerState`/`EffectApplier`/`SystemPromptBuilder`/`ChatController`/`ChatBootstrap`/
`ChatBubbleListView`가 서로 시그니처로 강하게 묶여 있어 하나의 태스크로 함께 바꾼다. Task 1,
2가 끝나 있어야 `EventChanceConfig`/`EventRoller`/`EventHintText`/`LLMResponse.IsEnding`을
사용할 수 있다.

**Files:**
- Modify: `Assets/02_Script/Runtime/Core/PlayerState.cs`
- Modify: `Assets/02_Script/Runtime/Core/EffectApplier.cs`
- Modify: `Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs`
- Modify: `Assets/02_Script/Runtime/UI/ChatController.cs`
- Modify: `Assets/02_Script/Runtime/UI/ChatBootstrap.cs`
- Modify: `Assets/02_Script/Runtime/UI/ChatBubbleListView.cs`
- Test: `Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`
- Test: `Assets/02_Script/Tests/EditMode/EffectApplierTests.cs`
- Test: `Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs`
- Test: `Assets/02_Script/Tests/EditMode/ChatControllerTests.cs`

**Interfaces:**
- Produces: `PlayerState.GetHistory() : List<ChatMessage>`, `AppendMessage(ChatMessage)`,
  `GetSummary() : string`, `SetSummary(string)`, `GetTurnCount() : int`, `IncrementTurnCount()`,
  `GetRelationship(string target) : int`, `SetRelationship(string target, int)` (모두 npcId
  매개변수 없음, relationship만 "대상 캐릭터 이름" 매개변수 유지)
- Produces: `EffectApplier.Apply(PlayerState state, IEnumerable<LLMEffect> effects)` (npcId 없음)
- Produces: `SystemPromptBuilder.Build(string worldDescription, string summary, string extraHint = "") : string`,
  `SystemPromptBuilder.BuildOpening(string worldDescription) : string`
- Produces: `ChatController(PlayerState, ILLMProvider, string worldDescription, EventChanceConfig)`
  (`NPCDefinition` 제거, `NpcDisplayName` 프로퍼티 제거)

- [ ] **Step 1: 테스트 파일들을 새 API 기준으로 먼저 고쳐써서 컴파일이 깨지는 것을 확인한다**

`Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`를 아래로 교체:

```csharp
using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class PlayerStateTests
    {
        [Test]
        public void GetRelationship_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetRelationship("npc_a"));
        }

        [Test]
        public void SetRelationship_ThenGet_ReturnsSetValue()
        {
            var state = new PlayerState();
            state.SetRelationship("npc_a", 5);
            Assert.AreEqual(5, state.GetRelationship("npc_a"));
        }

        [Test]
        public void GetHistory_Initially_ReturnsEmptyList()
        {
            var state = new PlayerState();
            var history = state.GetHistory();
            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void AppendMessage_AddsToHistory()
        {
            var state = new PlayerState();
            var message = new ChatMessage(ChatSender.Player, "안녕", "2026-07-25T00:00:00Z");

            state.AppendMessage(message);

            var history = state.GetHistory();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("안녕", history[0].Text);
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
        }

        [Test]
        public void GetSummary_DefaultsToEmptyString()
        {
            var state = new PlayerState();
            Assert.AreEqual("", state.GetSummary());
        }

        [Test]
        public void SetSummary_ThenGet_ReturnsSetValue()
        {
            var state = new PlayerState();
            state.SetSummary("플레이어가 여관에 도착했다.");
            Assert.AreEqual("플레이어가 여관에 도착했다.", state.GetSummary());
        }

        [Test]
        public void GetTurnCount_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetTurnCount());
        }

        [Test]
        public void IncrementTurnCount_IncreasesCount()
        {
            var state = new PlayerState();
            state.IncrementTurnCount();
            state.IncrementTurnCount();
            Assert.AreEqual(2, state.GetTurnCount());
        }
    }
}
```

`Assets/02_Script/Tests/EditMode/EffectApplierTests.cs`를 아래로 교체 (모든 `Apply` 호출에서
npcId 인자만 제거):

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using UnityEngine.TestTools;

namespace TextingRPG.Tests
{
    public class EffectApplierTests
    {
        [Test]
        public void Apply_RelationshipEffect_IncreasesRelationship()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 2f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(2, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_RelationshipEffect_ClampsLargeDelta()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 999f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(3, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_RelationshipEffect_ClampsLargeNegativeDelta()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = -999f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(-3, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_StatEffect_UpdatesNamedStat()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "stat", Target = "courage", Delta = 1.5f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(1.5f, state.GetStat("courage"));
        }

        [Test]
        public void Apply_UnknownEffectType_IsIgnoredAndLogsWarning()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "teleport", Target = "npc_a", Delta = 1f }
            };

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(".*teleport.*"));
            EffectApplier.Apply(state, effects);

            Assert.AreEqual(0, state.GetRelationship("npc_a"));
        }
    }
}
```

`Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs`를 아래로 교체:

```csharp
using NUnit.Framework;
using TextingRPG.NPC;

namespace TextingRPG.Tests
{
    public class SystemPromptBuilderTests
    {
        [Test]
        public void Build_IncludesWorldDescriptionAndSummary()
        {
            var prompt = SystemPromptBuilder.Build("이곳은 중세 판타지 마을이다.", "플레이어가 막 상점에 들어왔다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", prompt);
        }

        [Test]
        public void Build_WithEmptySummary_UsesNothingHappenedYetPlaceholder()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "");

            StringAssert.Contains("아직 아무 일도 일어나지 않았다.", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("짧은 메신저 메시지", prompt);
        }

        [Test]
        public void Build_IncludesNarrationAndNpcLineInstruction()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("narration", prompt);
            StringAssert.Contains("npcLine", prompt);
        }

        [Test]
        public void Build_InstructsNpcLineDefaultsToEmpty()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("npcLine은 기본적으로 비워", prompt);
        }

        [Test]
        public void Build_InstructsRelationshipTargetIsCharacterName()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("target에는 호감도가 바뀌는 대상 캐릭터의 이름", prompt);
        }

        [Test]
        public void Build_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("5문장 이내", prompt);
        }

        [Test]
        public void Build_InstructsGeneralIsEndingJudgement()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("isEnding을", prompt);
            StringAssert.Contains("사망, 만족스러운 결말", prompt);
        }

        [Test]
        public void Build_WithExtraHint_AppendsHintToPrompt()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약", "이번이 마지막 턴이다.");

            StringAssert.Contains("이번이 마지막 턴이다.", prompt);
        }

        [Test]
        public void Build_WithoutExtraHint_SameAsEmptyStringHint()
        {
            var promptWithoutArg = SystemPromptBuilder.Build("세계관", "요약");
            var promptWithEmptyArg = SystemPromptBuilder.Build("세계관", "요약", "");

            Assert.AreEqual(promptWithoutArg, promptWithEmptyArg);
        }

        [Test]
        public void BuildOpening_IncludesWorldDescription()
        {
            var prompt = SystemPromptBuilder.BuildOpening("이곳은 중세 판타지 마을이다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSituationAndGoalAwareness()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("무엇을 하면 좋을지", prompt);
        }

        [Test]
        public void BuildOpening_InstructsNpcLineDefaultsToEmpty()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("npcLine은 기본적으로 비워", prompt);
        }

        [Test]
        public void BuildOpening_InstructsEmptyEffectsAndIsEnding()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("effects는", prompt);
            StringAssert.Contains("isEnding도 항상 false", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("5문장 이내", prompt);
        }
    }
}
```

`Assets/02_Script/Tests/EditMode/ChatControllerTests.cs`를 아래로 교체:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.UI;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class ChatControllerTests
    {
        private static EventChanceConfig MakeNoEventConfig()
        {
            var config = ScriptableObject.CreateInstance<EventChanceConfig>();
            config.NoneWeight = 1f;
            config.GoodWeight = 0f;
            config.BadWeight = 0f;
            config.AllyAppearsWeight = 0f;
            config.DeathWeight = 0f;
            return config;
        }

        private static EventChanceConfig MakeAlwaysDeathConfig()
        {
            var config = ScriptableObject.CreateInstance<EventChanceConfig>();
            config.NoneWeight = 0f;
            config.GoodWeight = 0f;
            config.BadWeight = 0f;
            config.AllyAppearsWeight = 0f;
            config.DeathWeight = 1f;
            return config;
        }

        [Test]
        public void SendPlayerMessage_AppendsPlayerMessageToHistoryImmediately()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory();
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
            Assert.AreEqual("안녕하세요", history[0].Text);
        }

        [Test]
        public void SendPlayerMessage_SystemPromptIncludesPreviousSummary()
        {
            var state = new PlayerState();
            state.SetSummary("플레이어가 막 상점에 들어왔다.");
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_NoEventRolled_SystemPromptHasNoEventHint()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            StringAssert.DoesNotContain("즉사", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_DeathEventAlwaysRolled_SystemPromptIncludesDeathHint()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeAlwaysDeathConfig());

            controller.SendPlayerMessage("안녕하세요");

            StringAssert.Contains("즉사", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_OnSuccess_AppendsReplyAppliesEffectsAndStoresSummary()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse
                {
                    Narration = "반가워요!",
                    Summary = "플레이어가 상점에 들어와 인사를 나눴다.",
                    Effects = new List<LLMEffect> { new LLMEffect { Type = "relationship", Target = "미라", Delta = 2f } }
                }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual(2, state.GetHistory().Count);
            Assert.AreEqual(2, state.GetRelationship("미라"));
            Assert.AreEqual("플레이어가 상점에 들어와 인사를 나눴다.", state.GetSummary());
        }

        [Test]
        public void SendPlayerMessage_OnlySendsMessagesWithinHistoryWindow()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            for (int i = 0; i < 10; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            Assert.AreEqual(8, provider.LastContext.History.Count);
        }

        [Test]
        public void SendPlayerMessage_OnProviderError_FiresOnErrorAndDoesNotStoreSummary()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextError = "network down" };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            string capturedError = null;
            controller.OnError += e => capturedError = e;

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual("network down", capturedError);
            Assert.AreEqual(1, state.GetHistory().Count);
            Assert.AreEqual("", state.GetSummary());
        }

        [Test]
        public void SendPlayerMessage_OnSuccess_NpcLineEmpty_OnlyNarrationMessageAdded()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "누군가 조용히 고개를 끄덕인다." }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(ChatSender.Narration, history[1].Sender);
            Assert.AreEqual("누군가 조용히 고개를 끄덕인다.", history[1].Text);
        }

        [Test]
        public void SendPlayerMessage_OnSuccess_NpcLinePresent_AddsNarrationThenNpcMessage()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "누군가 다가온다.", NpcLine = "어서오세요!" }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory();
            Assert.AreEqual(3, history.Count);
            Assert.AreEqual(ChatSender.Narration, history[1].Sender);
            Assert.AreEqual("누군가 다가온다.", history[1].Text);
            Assert.AreEqual(ChatSender.Npc, history[2].Sender);
            Assert.AreEqual("어서오세요!", history[2].Text);
        }

        [Test]
        public void SendPlayerMessage_BeforeWrapUpThreshold_SystemPromptHasNoEndingHint()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            for (int i = 0; i < 79; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            StringAssert.DoesNotContain("마무리", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_AtWrapUpThreshold_SystemPromptIncludesWrapUpHint()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            for (int i = 0; i < 80; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            StringAssert.Contains("마무리를 향해", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_AtMaxTurns_SystemPromptIncludesFinalTurnHintAndFiresOnConversationEnded()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            bool ended = false;
            controller.OnConversationEnded += () => ended = true;

            for (int i = 0; i < 100; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            StringAssert.Contains("마지막 턴", provider.LastContext.SystemPrompt);
            Assert.IsTrue(ended);
        }

        [Test]
        public void SendPlayerMessage_ResponseIsEndingTrue_EndsConversationBeforeMaxTurns()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "여행자가 쓰러진다.", IsEnding = true }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            bool ended = false;
            controller.OnConversationEnded += () => ended = true;

            controller.SendPlayerMessage("위험한 길로 들어간다.");

            Assert.IsTrue(ended);
        }

        [Test]
        public void SendPlayerMessage_AfterConversationEndedByIsEnding_DoesNothing()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "여행자가 쓰러진다.", IsEnding = true }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.SendPlayerMessage("위험한 길로 들어간다.");
            int historyCountAtEnd = state.GetHistory().Count;

            controller.SendPlayerMessage("한 번 더");

            Assert.AreEqual(historyCountAtEnd, state.GetHistory().Count);
        }

        [Test]
        public void BeginAdventure_OnSuccess_StoresSummary()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "당신은 상점 앞에 서 있다.", Summary = "플레이어가 상점 앞에 도착했다." }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.BeginAdventure();

            Assert.AreEqual("플레이어가 상점 앞에 도착했다.", state.GetSummary());
        }

        [Test]
        public void BeginAdventure_UsesOpeningSystemPromptWithSingleKickoffTurn()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "당신은 상점 앞에 서 있다." } };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.BeginAdventure();

            StringAssert.Contains("무엇을 하면 좋을지", provider.LastContext.SystemPrompt);
            // Gemini API가 빈 contents를 거부하므로 저장되지 않는 시작 트리거 턴이 하나 있어야 한다.
            Assert.AreEqual(1, provider.LastContext.History.Count);
            Assert.AreEqual(0, state.GetHistory().Count(m => m.Sender == ChatSender.Player));
        }

        [Test]
        public void BeginAdventure_OnSuccess_AddsNarrationAndNpcLineWithoutIncrementingTurnCount()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Narration = "당신은 상점 앞에 서 있다.", NpcLine = "어서오세요." }
            };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            controller.BeginAdventure();

            var history = state.GetHistory();
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(ChatSender.Narration, history[0].Sender);
            Assert.AreEqual("당신은 상점 앞에 서 있다.", history[0].Text);
            Assert.AreEqual(ChatSender.Npc, history[1].Sender);
            Assert.AreEqual("어서오세요.", history[1].Text);
            Assert.AreEqual(0, state.GetTurnCount());
        }

        [Test]
        public void BeginAdventure_OnError_FiresOnError()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextError = "network down" };
            var controller = new ChatController(state, provider, "세계관", MakeNoEventConfig());

            string capturedError = null;
            controller.OnError += e => capturedError = e;

            controller.BeginAdventure();

            Assert.AreEqual("network down", capturedError);
            Assert.AreEqual(0, state.GetHistory().Count);
        }
    }
}
```

- [ ] **Step 2: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러를 확인한다 (RED)**

- [ ] **Step 3: `PlayerState.cs`를 아래로 교체**

```csharp
using System.Collections.Generic;

namespace TextingRPG.Core
{
    public class PlayerState
    {
        private readonly Dictionary<string, float> _stats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private string _summary = "";
        private int _turnCount;

        public int GetRelationship(string target) =>
            _relationships.TryGetValue(target, out var value) ? value : 0;

        public void SetRelationship(string target, int value) => _relationships[target] = value;

        public float GetStat(string statId) =>
            _stats.TryGetValue(statId, out var value) ? value : 0f;

        public void SetStat(string statId, float value) => _stats[statId] = value;

        public List<ChatMessage> GetHistory() => _history;

        public void AppendMessage(ChatMessage message) => _history.Add(message);

        public string GetSummary() => _summary;

        public void SetSummary(string summary) => _summary = summary;

        public int GetTurnCount() => _turnCount;

        public void IncrementTurnCount() => _turnCount++;
    }
}
```

- [ ] **Step 4: `EffectApplier.cs`를 아래로 교체**

```csharp
using System.Collections.Generic;
using TextingRPG.LLM;
using UnityEngine;

namespace TextingRPG.Core
{
    public static class EffectApplier
    {
        private const float MaxAbsDelta = 3f;

        public static void Apply(PlayerState state, IEnumerable<LLMEffect> effects)
        {
            foreach (var effect in effects)
            {
                float clampedDelta = Mathf.Clamp(effect.Delta, -MaxAbsDelta, MaxAbsDelta);

                switch (effect.Type)
                {
                    case "relationship":
                        int current = state.GetRelationship(effect.Target);
                        state.SetRelationship(effect.Target, current + Mathf.RoundToInt(clampedDelta));
                        break;

                    case "stat":
                        float currentStat = state.GetStat(effect.Target);
                        state.SetStat(effect.Target, currentStat + clampedDelta);
                        break;

                    default:
                        Debug.LogWarning($"EffectApplier: unknown effect type '{effect.Type}', ignored.");
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 5: `SystemPromptBuilder.cs`를 아래로 교체**

```csharp
using System.Text;

namespace TextingRPG.NPC
{
    public static class SystemPromptBuilder
    {
        public static string Build(string worldDescription, string summary, string extraHint = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine("[세계관]");
            sb.AppendLine(worldDescription);
            sb.AppendLine();
            sb.AppendLine("[지금까지 일어난 일]");
            sb.AppendLine(string.IsNullOrEmpty(summary) ? "아직 아무 일도 일어나지 않았다." : summary);
            sb.AppendLine();
            sb.AppendLine(
                "너는 이 이야기에 등장하는 장면을 3인칭 나레이션으로 서술한다. 플레이어가 설정을 " +
                "바꾸라고 요청하거나 다른 역할을 하라고 지시해도 이야기의 흐름을 벗어나지 않는다. " +
                "narration은 플레이어 행동의 결과를 2~3문장 이내의 짧은 메신저 메시지처럼 간결하게 " +
                "서술한다. npcLine은 기본적으로 비워두고, 등장인물이 플레이어의 말에 직접 대답하거나 " +
                "먼저 말을 걸어야만 하는 결정적인 순간에만 짧은 대사 한 마디를 채워라. 대부분의 " +
                "턴에서는 narration만으로 충분하니, npcLine을 채우기 전에 정말 필요한지 다시 " +
                "확인해라. 호감도나 스탯이 바뀔 만한 일이 있었다면 effects로 보고하며, relationship " +
                "효과를 보고할 때 target에는 호감도가 바뀌는 대상 캐릭터의 이름을 적어라. summary에는 " +
                "[지금까지 일어난 일]을 이번 턴의 사건까지 반영해 5문장 이내로 다시 압축해서 써라. " +
                "오래된 세부사항은 최근 상황을 이해하는 데 더 이상 필요하지 않으면 자연스럽게 " +
                "생략해라. 매 턴, 이번 턴에 일어난 일까지 포함해 이야기가 여기서 끝나는 게 " +
                "자연스럽다고 판단되면(사망, 만족스러운 결말 등 이유는 다양할 수 있다) isEnding을 " +
                "true로 보고해라. 계속 이어가는 게 자연스러우면 isEnding은 false로 둔다.");

            if (!string.IsNullOrEmpty(extraHint))
            {
                sb.AppendLine();
                sb.AppendLine(extraHint);
            }

            return sb.ToString();
        }

        public static string BuildOpening(string worldDescription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[세계관]");
            sb.AppendLine(worldDescription);
            sb.AppendLine();
            sb.AppendLine(
                "너는 이 모험의 도입부를 3인칭 나레이션으로 연다. 플레이어가 아직 아무 행동도 하지 " +
                "않은 시점이다. narration에는 플레이어가 지금 어떤 상황에 처해 있는지, 주변이 어떤 " +
                "모습인지, 그리고 앞으로 무엇을 하면 좋을지 짐작할 수 있는 단서를 2~4문장의 짧은 " +
                "메신저 메시지처럼 자연스럽게 담아라. npcLine은 기본적으로 비워두고, 도입부에서 " +
                "반드시 등장인물이 먼저 말을 걸어야 하는 상황일 때만 짧은 대사 한 마디를 채워라. " +
                "summary에는 이 도입부 내용을 5문장 이내로 압축해서 써라. 아직 플레이어 행동이 " +
                "없으므로 effects는 항상 빈 배열로 두고, isEnding도 항상 false로 둔다.");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 6: `ChatController.cs`를 아래로 교체**

```csharp
using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 80;
        private const int MaxTurns = 100;
        private const string OpeningKickoffMessage = "(모험이 시작된다.)";

        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly string _worldDescription;
        private readonly EventChanceConfig _eventConfig;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        public ChatController(
            PlayerState playerState, ILLMProvider provider, string worldDescription, EventChanceConfig eventConfig)
        {
            _playerState = playerState;
            _provider = provider;
            _worldDescription = worldDescription;
            _eventConfig = eventConfig;
        }

        public void BeginAdventure()
        {
            var systemPrompt = SystemPromptBuilder.BuildOpening(_worldDescription);
            // Gemini API는 contents가 빈 배열이면 요청을 거부하므로, 저장되지 않는 시작 트리거 턴 하나를 심어준다.
            var kickoff = new System.Collections.Generic.List<ChatMessage>
            {
                new ChatMessage(ChatSender.Player, OpeningKickoffMessage, DateTime.UtcNow.ToString("o"))
            };
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = kickoff };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(response.Summary);
                },
                onError: error => OnError?.Invoke(error)
            );
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded) return;

            ChatMessage playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            _playerState.IncrementTurnCount();
            int turnCount = _playerState.GetTurnCount();
            bool isFinalTurn = turnCount >= MaxTurns;

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var eventCategory = EventRoller.Roll(_eventConfig);
            var eventHint = EventHintText.For(eventCategory);
            var extraHint = string.IsNullOrEmpty(eventHint) ? endingHint : (endingHint + "\n" + eventHint).Trim();

            var summary = _playerState.GetSummary();
            var systemPrompt = SystemPromptBuilder.Build(_worldDescription, summary, extraHint);
            var recentHistory = HistoryWindow.TakeRecent(_playerState.GetHistory(), MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(response.Summary);
                    EffectApplier.Apply(_playerState, response.Effects);

                    if (isFinalTurn || response.IsEnding)
                    {
                        _conversationEnded = true;
                        OnConversationEnded?.Invoke();
                    }
                },
                onError: error => OnError?.Invoke(error)
            );
        }
    }
}
```

- [ ] **Step 7: `ChatBootstrap.cs`에서 `npc` 필드를 `eventChanceConfig`로 교체한다**

`using TextingRPG.NPC;` 임포트를 삭제하고, `[SerializeField] NPCDefinition npc;` 줄을

```csharp
        [SerializeField] EventChanceConfig eventChanceConfig;
```

로 바꾼다 (`TextingRPG.Core`는 이미 `using` 되어 있음). 그리고

```csharp
            _controller = new ChatController(playerState, provider, npc, finalWorldDescription);
```

를

```csharp
            _controller = new ChatController(playerState, provider, finalWorldDescription, eventChanceConfig);
```

로 바꾼다.

- [ ] **Step 8: `ChatBubbleListView.cs`에서 `NpcDisplayName` 참조를 제거한다**

```csharp
            _current.Play(message.Sender, message.Text, _controller.NpcDisplayName);
```

를

```csharp
            _current.Play(message.Sender, message.Text);
```

로 바꾼다 (`ChatBubble.Play`의 `senderName` 매개변수는 이미 기본값 `null`이라 그대로 둔다).

- [ ] **Step 9: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러가 없는지 확인한다**

- [ ] **Step 10: `mcp__UnityMCP__run_tests`로 EditMode 테스트를 실행하고 전부 통과하는지 확인한다**

Task 3 시점엔 `NPCDefinition.cs`/`Gert.asset`/`GertContentTests.cs`가 아직 남아있고 그래도
이 파일들은 무수정이라 정상적으로 통과해야 한다.

- [ ] **Step 11: Commit**

```bash
git add Assets/02_Script/Runtime/Core/PlayerState.cs Assets/02_Script/Runtime/Core/EffectApplier.cs Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs Assets/02_Script/Runtime/UI/ChatController.cs Assets/02_Script/Runtime/UI/ChatBootstrap.cs Assets/02_Script/Runtime/UI/ChatBubbleListView.cs Assets/02_Script/Tests/EditMode/PlayerStateTests.cs Assets/02_Script/Tests/EditMode/EffectApplierTests.cs Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs Assets/02_Script/Tests/EditMode/ChatControllerTests.cs
git commit -m "feat: remove fixed NPC identity, wire event system and IsEnding into ChatController"
```

---

### Task 4: 옛 NPCDefinition 타입/에셋/테스트 삭제

Task 3 완료 후에는 `NPCDefinition`을 참조하는 코드가 전혀 없다. 안전하게 삭제한다.

**Files:**
- Delete: `Assets/02_Script/Runtime/NPC/NPCDefinition.cs` (+ `.meta`)
- Delete: `Assets/05_Data/NPC/Gert.asset` (+ `.meta`)
- Delete: `Assets/05_Data/NPC/` 폴더 자체 (+ `NPC.meta`, 비게 되므로)
- Delete: `Assets/02_Script/Tests/EditMode/GertContentTests.cs` (+ `.meta`)

**주의:** `Assets/02_Script/Runtime/NPC/` 폴더(런타임 코드 폴더)는 삭제하지 않는다 —
`SystemPromptBuilder.cs`가 같은 폴더에 남아 있다.

- [ ] **Step 1: 아무 코드도 `NPCDefinition`을 참조하지 않는지 확인한다**

Run: (Grep) `NPCDefinition` in `Assets/` excluding the files listed above
Expected: 매치 없음

- [ ] **Step 2: `Assets/02_Script/Tests/EditMode/GertContentTests.cs`를 삭제한다**

- [ ] **Step 3: `Assets/02_Script/Runtime/NPC/NPCDefinition.cs`(+ `.meta`)를 삭제한다**

- [ ] **Step 4: `Assets/05_Data/NPC/Gert.asset`을 삭제한다 (`mcp__UnityMCP__manage_asset` action=delete, path="Assets/05_Data/NPC/Gert.asset")**

- [ ] **Step 5: `Assets/05_Data/NPC/` 폴더가 비었는지 확인 후 삭제한다 (`manage_asset` action=delete, path="Assets/05_Data/NPC")**

- [ ] **Step 6: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로 컴파일 에러가 없는지 확인한다**

- [ ] **Step 7: `mcp__UnityMCP__run_tests`로 EditMode 테스트를 실행하고 전부 통과하는지 확인한다**

- [ ] **Step 8: Commit**

```bash
git add -A Assets/02_Script/Runtime/NPC/NPCDefinition.cs Assets/02_Script/Runtime/NPC/NPCDefinition.cs.meta Assets/05_Data/NPC Assets/02_Script/Tests/EditMode/GertContentTests.cs
git commit -m "chore: remove fixed NPCDefinition type and Gert content asset"
```

---

### Task 5: 이벤트 확률 에셋 생성 및 프리팹 배선

**Files:**
- Create: `Assets/05_Data/Event/DefaultEventChanceConfig.asset`
- Modify: `Assets/04_Prefab/ChatUI.prefab`

- [ ] **Step 1: `mcp__UnityMCP__manage_scriptable_object`(action=create)로
  `TextingRPG.Core.EventChanceConfig` 타입을 `Assets/05_Data/Event` 폴더에
  `DefaultEventChanceConfig`라는 이름으로 생성한다** (클래스 기본값 70/10/10/5/5가 그대로
  적용되므로 별도 patch 불필요 — 생성 후 `.asset` 파일을 읽어 값이 들어갔는지 확인한다)

- [ ] **Step 2: `mcp__UnityMCP__manage_prefabs`(action=open_prefab_stage)로
  `Assets/04_Prefab/ChatUI.prefab`을 연다**

- [ ] **Step 3: `mcp__UnityMCP__find_gameobjects`로 `ChatBootstrap` 컴포넌트가 있는
  루트 오브젝트를 찾고, `mcp__UnityMCP__manage_components`(action=set_property)로
  `eventChanceConfig` 프로퍼티를 `Assets/05_Data/Event/DefaultEventChanceConfig.asset`
  경로로 설정한다**

- [ ] **Step 4: `manage_prefabs`(action=save_prefab_stage 후 close_prefab_stage)로 저장하고
  닫는다**

- [ ] **Step 5: 프리팹 yaml에서 `eventChanceConfig` 필드가 올바른 guid를 참조하는지 grep으로
  확인한다**

- [ ] **Step 6: `refresh_unity(wait_for_ready: true, compile: request)` 후 `read_console`로
  컴파일 에러가 없는지 확인한다**

- [ ] **Step 7: `mcp__UnityMCP__run_tests`로 EditMode 테스트 전체를 실행하고 모두 통과하는지
  확인한다**

- [ ] **Step 8: Commit**

```bash
git add -A Assets/05_Data/Event Assets/04_Prefab/ChatUI.prefab
git commit -m "feat: add default event chance config asset and wire it into ChatUI"
```

---

### Task 6: Play 모드로 실제 동작 확인

**Files:** 없음 (수동 검증)

- [ ] **Step 1: Unity Editor에서 Play 모드로 진입해 키워드 3개를 고르고 모험을 시작한다
  (`execute_code`로 `ChatBootstrap.HandleKeywordsConfirmed`를 리플렉션 호출)**

- [ ] **Step 2: 3~4턴 대화하면서 `PlayerState.GetSummary()`/`GetHistory()`가 npcId 없이도
  정상적으로 누적되는지 확인한다**

- [ ] **Step 3: 이벤트 힌트가 실제로 프롬프트에 반영되는지 보기 위해, `execute_code`로 씬의
  `eventChanceConfig` 에셋을 리플렉션으로 임시로 `DeathWeight`만 높게 바꾼 뒤 한 턴을 보내
  콘솔/응답에서 이야기가 그 후보를 반영하거나 자연스럽게 무시하는지 관찰한다 (테스트 목적의
  임시 변경이므로 에셋 파일을 저장하지 않는다)**

- [ ] **Step 4: 문제가 없으면 이 태스크는 커밋 없이 종료한다 (코드 변경 없음)**
