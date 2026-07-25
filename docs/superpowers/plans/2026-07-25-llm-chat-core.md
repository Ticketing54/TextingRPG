# LLM 대화 핵심 (Plan 1 of 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어가 메신저 스타일 UI로 NPC와 자유 텍스트로 대화하면, Claude API가 실시간으로 응답을 생성하고 그 결과(호감도/스탯 변화)가 PlayerState에 반영되는 최소 동작 파이프라인을 만든다.

**Architecture:** `ILLMProvider` 인터페이스로 LLM 호출을 추상화하고 (`ClaudeProvider` 실구현 + `MockLLMProvider` 테스트용 구현), `ChatController`가 `PlayerState` ↔ `ILLMProvider` ↔ `EffectApplier`를 오케스트레이션한다. UI(`ChatUIView`)는 `ChatController`가 발행하는 이벤트만 구독하는 얇은 레이어로 둔다.

**Tech Stack:** Unity 6000.3.10f1, C# (Unity 표준 Roslyn 컴파일러), Unity uGUI + TextMeshPro (UI), Unity Test Framework (NUnit, EditMode), UnityWebRequest (네트워킹), Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`, JSON 직렬화 — Task 6에서 추가), Claude Messages API (Anthropic).

## Global Constraints

- 대상 플랫폼: Windows 스탠드얼론 (PC). 클라이언트에서 Claude API를 직접 호출한다.
- LLM 제공자: 지금은 Claude API. `ILLMProvider` 아래로만 의존하고, 위쪽 코드(`ChatController`, UI)는 구현체를 몰라야 한다 (인터페이스 경계 유지 — 나중에 로컬 LLM으로 교체 가능해야 함).
- API 키는 절대 코드/에셋에 하드코딩하거나 커밋하지 않는다. 환경 변수 `ANTHROPIC_API_KEY`에서 읽는다.
- LLM 응답의 `effects`는 게임 코드(`EffectApplier`)가 화이트리스트 검증 후 클램프해서 반영한다. LLM이 준 값을 그대로 신뢰하지 않는다.
- UI는 uGUI(+TextMeshPro)를 사용한다 (UI Toolkit 아님 — 프로젝트에 `com.unity.ugui`만 실제로 쓰이고 있음).
- 네임스페이스 루트는 `TextingRPG`이며, 런타임 코드와 EditMode 테스트는 별도 asmdef로 분리한다.
- 전투/인벤토리/장르 프리셋/세이브는 이 플랜의 범위 밖이다 (Plan 2, Plan 3에서 다룸). 이 플랜은 NPC 1명 + 호감도 하나만으로 끝까지 동작하는 걸 목표로 한다.

**이번 플랜에서 의도적으로 미루는 것 (설계서의 하드닝 항목 중 일부):**
- 대화 히스토리 truncate/요약 (토큰 예산 관리) — 지금은 전체 히스토리를 그대로 보낸다. 대화가 길어지는 콘텐츠를 붙이는 후속 작업에서 다룬다.
- 스키마 검증 실패 시 "더 엄격한 재프롬프트로 재시도" — 지금은 파싱 실패 시 바로 `onError`로 폴백한다 (네트워크 실패는 1회 재시도하지만, 파싱 실패까지 재시도하려면 재프롬프트 문구 설계가 별도로 필요해서 범위에서 뺐다).
- 세션당 요청 쿨다운/비용 제한 — 지금은 없음. Plan 2/3 진행 전에 실사용 API 비용을 보고 우선순위를 다시 정한다.

---

## File Structure

```
Assets/02_Script/
  Runtime/
    TextingRPG.Runtime.asmdef
    Core/
      ChatMessage.cs
      PlayerState.cs
      EffectApplier.cs
    LLM/
      LLMEffect.cs
      LLMResponse.cs
      ConversationContext.cs
      ILLMProvider.cs
      MockLLMProvider.cs
      ClaudeProvider.cs
    NPC/
      NPCDefinition.cs
      SystemPromptBuilder.cs
    UI/
      ChatController.cs
      ChatUIView.cs
      ChatMessageView.cs
  Tests/
    EditMode/
      TextingRPG.EditModeTests.asmdef
      PlayerStateTests.cs
      EffectApplierTests.cs
      MockLLMProviderTests.cs
      ChatControllerTests.cs
      ClaudeProviderTests.cs
      SystemPromptBuilderTests.cs
Assets/01_Scene/
  ChatDemo.unity   (Task 9에서 생성)
```

---

### Task 1: 테스트 인프라 (asmdef 분리)

**Files:**
- Create: `Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef`
- Create: `Assets/02_Script/Tests/EditMode/TextingRPG.EditModeTests.asmdef`

**Interfaces:**
- Produces: 어셈블리 이름 `TextingRPG.Runtime` (모든 런타임 코드가 여기 소속), `TextingRPG.EditModeTests` (모든 EditMode 테스트가 여기 소속, `TextingRPG.Runtime` 참조)

- [ ] **Step 1: 폴더 및 Runtime asmdef 생성**

`Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef`:

```json
{
    "name": "TextingRPG.Runtime",
    "rootNamespace": "TextingRPG",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: EditMode 테스트 asmdef 생성**

`Assets/02_Script/Tests/EditMode/TextingRPG.EditModeTests.asmdef`:

```json
{
    "name": "TextingRPG.EditModeTests",
    "rootNamespace": "TextingRPG.Tests",
    "references": [
        "TextingRPG.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Unity 에디터에서 컴파일 확인**

Unity 에디터를 열고 (이미 열려 있다면 포커스만 줘도 자동 리컴파일) 콘솔에 에러가 없는지 확인. `Window > General > Test Runner`를 열어 `EditMode` 탭에 `TextingRPG.EditModeTests` 어셈블리가 인식되는지 확인 (테스트가 아직 없으므로 트리는 비어있는 게 정상).

- [ ] **Step 4: 커밋**

```bash
git add Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef.meta Assets/02_Script/Tests/EditMode/TextingRPG.EditModeTests.asmdef Assets/02_Script/Tests/EditMode/TextingRPG.EditModeTests.asmdef.meta
git commit -m "chore: add Runtime/EditModeTests assembly definitions"
```

(Unity가 `.meta` 파일을 자동 생성하므로, 커밋 전에 `git status`로 실제 생성된 `.meta` 파일명을 확인할 것.)

---

### Task 2: 핵심 데이터 모델 — ChatMessage, PlayerState

**Files:**
- Create: `Assets/02_Script/Runtime/Core/ChatMessage.cs`
- Create: `Assets/02_Script/Runtime/Core/PlayerState.cs`
- Test: `Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`

**Interfaces:**
- Produces:
  - `enum ChatSender { Player, Npc }`
  - `class ChatMessage(ChatSender sender, string text, string timestampIso8601)` — 필드 `Sender`, `Text`, `Timestamp`
  - `class PlayerState` — `int GetRelationship(string npcId)`, `void SetRelationship(string npcId, int value)`, `float GetStat(string statId)`, `void SetStat(string statId, float value)`, `List<ChatMessage> GetHistory(string npcId)`, `void AppendMessage(string npcId, ChatMessage message)`

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`:

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
        public void GetHistory_ForUnknownNpc_ReturnsEmptyList()
        {
            var state = new PlayerState();
            var history = state.GetHistory("npc_a");
            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void AppendMessage_AddsToHistoryForThatNpc()
        {
            var state = new PlayerState();
            var message = new ChatMessage(ChatSender.Player, "안녕", "2026-07-25T00:00:00Z");

            state.AppendMessage("npc_a", message);

            var history = state.GetHistory("npc_a");
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("안녕", history[0].Text);
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
        }

        [Test]
        public void AppendMessage_DoesNotAffectOtherNpcsHistory()
        {
            var state = new PlayerState();
            state.AppendMessage("npc_a", new ChatMessage(ChatSender.Player, "hi a", "t"));

            Assert.AreEqual(0, state.GetHistory("npc_b").Count);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 컴파일 실패 확인**

Unity Test Runner (`Window > General > Test Runner` → `EditMode` 탭 → `Run All`)를 실행. `TextingRPG.Core` 네임스페이스가 없어서 컴파일 에러가 뜨는 것을 확인.

- [ ] **Step 3: ChatMessage 구현**

`Assets/02_Script/Runtime/Core/ChatMessage.cs`:

```csharp
namespace TextingRPG.Core
{
    public enum ChatSender
    {
        Player,
        Npc
    }

    [System.Serializable]
    public class ChatMessage
    {
        public ChatSender Sender;
        public string Text;
        public string Timestamp;

        public ChatMessage(ChatSender sender, string text, string timestamp)
        {
            Sender = sender;
            Text = text;
            Timestamp = timestamp;
        }
    }
}
```

- [ ] **Step 4: PlayerState 구현**

`Assets/02_Script/Runtime/Core/PlayerState.cs`:

```csharp
using System.Collections.Generic;

namespace TextingRPG.Core
{
    public class PlayerState
    {
        private readonly Dictionary<string, float> _stats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private readonly Dictionary<string, List<ChatMessage>> _conversationHistories =
            new Dictionary<string, List<ChatMessage>>();

        public int GetRelationship(string npcId) =>
            _relationships.TryGetValue(npcId, out var value) ? value : 0;

        public void SetRelationship(string npcId, int value) => _relationships[npcId] = value;

        public float GetStat(string statId) =>
            _stats.TryGetValue(statId, out var value) ? value : 0f;

        public void SetStat(string statId, float value) => _stats[statId] = value;

        public List<ChatMessage> GetHistory(string npcId)
        {
            if (!_conversationHistories.TryGetValue(npcId, out var history))
            {
                history = new List<ChatMessage>();
                _conversationHistories[npcId] = history;
            }
            return history;
        }

        public void AppendMessage(string npcId, ChatMessage message)
        {
            GetHistory(npcId).Add(message);
        }
    }
}
```

- [ ] **Step 5: 테스트 실행 → 통과 확인**

Test Runner에서 `PlayerStateTests`의 5개 테스트가 모두 초록불(Pass)인지 확인.

- [ ] **Step 6: 커밋**

```bash
git add Assets/02_Script/Runtime/Core/ChatMessage.cs Assets/02_Script/Runtime/Core/ChatMessage.cs.meta Assets/02_Script/Runtime/Core/PlayerState.cs Assets/02_Script/Runtime/Core/PlayerState.cs.meta Assets/02_Script/Tests/EditMode/PlayerStateTests.cs Assets/02_Script/Tests/EditMode/PlayerStateTests.cs.meta
git commit -m "feat: add ChatMessage and PlayerState core data model"
```

---

### Task 3: EffectApplier + LLMEffect

**Files:**
- Create: `Assets/02_Script/Runtime/LLM/LLMEffect.cs`
- Create: `Assets/02_Script/Runtime/Core/EffectApplier.cs`
- Test: `Assets/02_Script/Tests/EditMode/EffectApplierTests.cs`

**Interfaces:**
- Consumes: `TextingRPG.Core.PlayerState` (Task 2) — `GetRelationship/SetRelationship/GetStat/SetStat`
- Produces:
  - `class LLMEffect { string Type; string Target; float Delta; }`
  - `static class EffectApplier { static void Apply(PlayerState state, string npcId, IEnumerable<LLMEffect> effects) }` — 지원 `Type`: `"relationship"` (해당 npcId의 호감도 변경), `"stat"` (Target을 statId로 사용해 PlayerState 스탯 변경). 그 외 타입은 무시 + `Debug.LogWarning`. Delta는 항상 `[-3, 3]`으로 클램프.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/EffectApplierTests.cs`:

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

            EffectApplier.Apply(state, "npc_a", effects);

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

            EffectApplier.Apply(state, "npc_a", effects);

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

            EffectApplier.Apply(state, "npc_a", effects);

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

            EffectApplier.Apply(state, "npc_a", effects);

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
            EffectApplier.Apply(state, "npc_a", effects);

            Assert.AreEqual(0, state.GetRelationship("npc_a"));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

Test Runner 실행. `TextingRPG.LLM` 네임스페이스와 `EffectApplier`가 없어 컴파일 에러 발생 확인.

- [ ] **Step 3: LLMEffect 구현**

`Assets/02_Script/Runtime/LLM/LLMEffect.cs`:

```csharp
namespace TextingRPG.LLM
{
    [System.Serializable]
    public class LLMEffect
    {
        public string Type;
        public string Target;
        public float Delta;
    }
}
```

- [ ] **Step 4: EffectApplier 구현**

`Assets/02_Script/Runtime/Core/EffectApplier.cs`:

```csharp
using System.Collections.Generic;
using TextingRPG.LLM;
using UnityEngine;

namespace TextingRPG.Core
{
    public static class EffectApplier
    {
        private const float MaxAbsDelta = 3f;

        public static void Apply(PlayerState state, string npcId, IEnumerable<LLMEffect> effects)
        {
            foreach (var effect in effects)
            {
                float clampedDelta = Mathf.Clamp(effect.Delta, -MaxAbsDelta, MaxAbsDelta);

                switch (effect.Type)
                {
                    case "relationship":
                        int current = state.GetRelationship(npcId);
                        state.SetRelationship(npcId, current + Mathf.RoundToInt(clampedDelta));
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

- [ ] **Step 5: 테스트 실행 → 통과 확인**

Test Runner에서 `EffectApplierTests`의 5개 테스트가 모두 통과하는지 확인.

- [ ] **Step 6: 커밋**

```bash
git add Assets/02_Script/Runtime/LLM/LLMEffect.cs Assets/02_Script/Runtime/LLM/LLMEffect.cs.meta Assets/02_Script/Runtime/Core/EffectApplier.cs Assets/02_Script/Runtime/Core/EffectApplier.cs.meta Assets/02_Script/Tests/EditMode/EffectApplierTests.cs Assets/02_Script/Tests/EditMode/EffectApplierTests.cs.meta
git commit -m "feat: add EffectApplier with whitelisted, clamped effect handling"
```

---

### Task 4: LLM 계약 — ConversationContext, LLMResponse, ILLMProvider, MockLLMProvider

**Files:**
- Create: `Assets/02_Script/Runtime/LLM/ConversationContext.cs`
- Create: `Assets/02_Script/Runtime/LLM/LLMResponse.cs`
- Create: `Assets/02_Script/Runtime/LLM/ILLMProvider.cs`
- Create: `Assets/02_Script/Runtime/LLM/MockLLMProvider.cs`
- Test: `Assets/02_Script/Tests/EditMode/MockLLMProviderTests.cs`

**Interfaces:**
- Consumes: `TextingRPG.Core.ChatMessage` (Task 2), `TextingRPG.LLM.LLMEffect` (Task 3)
- Produces:
  - `class ConversationContext { string SystemPrompt; List<ChatMessage> History; }` — `History`의 마지막 항목이 플레이어가 방금 보낸 메시지
  - `class LLMResponse { string Reply; List<LLMEffect> Effects; }`
  - `interface ILLMProvider { void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError); }`
  - `class MockLLMProvider : ILLMProvider` — 필드 `LLMResponse NextResponse`, `ConversationContext LastContext { get; }`. `SendMessage` 호출 시 동기적으로 `LastContext`를 기록하고 `onSuccess(NextResponse)`를 즉시 호출. `NextError`가 설정되어 있으면 `onError(NextError)`를 대신 호출.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/MockLLMProviderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.LLM;

namespace TextingRPG.Tests
{
    public class MockLLMProviderTests
    {
        [Test]
        public void SendMessage_InvokesOnSuccessWithNextResponse()
        {
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Reply = "안녕!", Effects = new List<LLMEffect>() }
            };
            var context = new ConversationContext { SystemPrompt = "test", History = new List<Core.ChatMessage>() };

            LLMResponse received = null;
            provider.SendMessage(context, r => received = r, e => Assert.Fail("onError should not be called"));

            Assert.AreEqual("안녕!", received.Reply);
        }

        [Test]
        public void SendMessage_RecordsLastContext()
        {
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "x", Effects = new List<LLMEffect>() } };
            var context = new ConversationContext { SystemPrompt = "system-prompt-xyz", History = new List<Core.ChatMessage>() };

            provider.SendMessage(context, _ => { }, _ => { });

            Assert.AreEqual("system-prompt-xyz", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendMessage_WithNextErrorSet_InvokesOnErrorInstead()
        {
            var provider = new MockLLMProvider { NextError = "network down" };
            var context = new ConversationContext { SystemPrompt = "s", History = new List<Core.ChatMessage>() };

            string receivedError = null;
            provider.SendMessage(context, _ => Assert.Fail("onSuccess should not be called"), e => receivedError = e);

            Assert.AreEqual("network down", receivedError);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

Test Runner 실행, `TextingRPG.LLM.ConversationContext`/`LLMResponse`/`MockLLMProvider`가 없어 컴파일 에러 발생 확인.

- [ ] **Step 3: ConversationContext, LLMResponse 구현**

`Assets/02_Script/Runtime/LLM/ConversationContext.cs`:

```csharp
using System.Collections.Generic;
using TextingRPG.Core;

namespace TextingRPG.LLM
{
    public class ConversationContext
    {
        public string SystemPrompt;
        public List<ChatMessage> History;
    }
}
```

`Assets/02_Script/Runtime/LLM/LLMResponse.cs`:

```csharp
using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class LLMResponse
    {
        public string Reply;
        public List<LLMEffect> Effects = new List<LLMEffect>();
    }
}
```

- [ ] **Step 4: ILLMProvider 인터페이스 구현**

`Assets/02_Script/Runtime/LLM/ILLMProvider.cs`:

```csharp
using System;

namespace TextingRPG.LLM
{
    public interface ILLMProvider
    {
        void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError);
    }
}
```

- [ ] **Step 5: MockLLMProvider 구현**

`Assets/02_Script/Runtime/LLM/MockLLMProvider.cs`:

```csharp
using System;

namespace TextingRPG.LLM
{
    public class MockLLMProvider : ILLMProvider
    {
        public LLMResponse NextResponse;
        public string NextError;
        public ConversationContext LastContext { get; private set; }

        public void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError)
        {
            LastContext = context;

            if (NextError != null)
            {
                onError?.Invoke(NextError);
                return;
            }

            onSuccess?.Invoke(NextResponse);
        }
    }
}
```

- [ ] **Step 6: 테스트 실행 → 통과 확인**

Test Runner에서 `MockLLMProviderTests`의 3개 테스트가 모두 통과하는지 확인.

- [ ] **Step 7: 커밋**

```bash
git add Assets/02_Script/Runtime/LLM/ConversationContext.cs Assets/02_Script/Runtime/LLM/ConversationContext.cs.meta Assets/02_Script/Runtime/LLM/LLMResponse.cs Assets/02_Script/Runtime/LLM/LLMResponse.cs.meta Assets/02_Script/Runtime/LLM/ILLMProvider.cs Assets/02_Script/Runtime/LLM/ILLMProvider.cs.meta Assets/02_Script/Runtime/LLM/MockLLMProvider.cs Assets/02_Script/Runtime/LLM/MockLLMProvider.cs.meta Assets/02_Script/Tests/EditMode/MockLLMProviderTests.cs Assets/02_Script/Tests/EditMode/MockLLMProviderTests.cs.meta
git commit -m "feat: add ILLMProvider contract and MockLLMProvider test double"
```

---

### Task 5: ChatController (오케스트레이션)

**Files:**
- Create: `Assets/02_Script/Runtime/UI/ChatController.cs`
- Test: `Assets/02_Script/Tests/EditMode/ChatControllerTests.cs`

**Interfaces:**
- Consumes: `PlayerState` (Task 2), `EffectApplier.Apply` (Task 3), `ILLMProvider`/`MockLLMProvider`/`ConversationContext`/`LLMResponse` (Task 4)
- Produces: `class ChatController(PlayerState playerState, ILLMProvider provider, string npcId, string systemPrompt)` — `void SendPlayerMessage(string text)`, `event Action<ChatMessage> OnMessageAdded`, `event Action<string> OnError`. Task 8(UI)이 이 이벤트들을 구독한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/ChatControllerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.UI;

namespace TextingRPG.Tests
{
    public class ChatControllerTests
    {
        [Test]
        public void SendPlayerMessage_AppendsPlayerMessageToHistoryImmediately()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok", Effects = new List<LLMEffect>() } };
            var controller = new ChatController(state, provider, "npc_a", "system prompt");

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory("npc_a");
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
            Assert.AreEqual("안녕하세요", history[0].Text);
        }

        [Test]
        public void SendPlayerMessage_OnProviderSuccess_AppendsNpcReplyAndAppliesEffects()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse
                {
                    Reply = "반가워요!",
                    Effects = new List<LLMEffect> { new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 2f } }
                }
            };
            var controller = new ChatController(state, provider, "npc_a", "system prompt");

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory("npc_a");
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(ChatSender.Npc, history[1].Sender);
            Assert.AreEqual("반가워요!", history[1].Text);
            Assert.AreEqual(2, state.GetRelationship("npc_a"));
        }

        [Test]
        public void SendPlayerMessage_SendsFullHistoryAndSystemPromptToProvider()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok", Effects = new List<LLMEffect>() } };
            var controller = new ChatController(state, provider, "npc_a", "너는 친절한 상인이다");

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual("너는 친절한 상인이다", provider.LastContext.SystemPrompt);
            Assert.AreEqual(1, provider.LastContext.History.Count);
            Assert.AreEqual("안녕하세요", provider.LastContext.History[0].Text);
        }

        [Test]
        public void SendPlayerMessage_OnProviderError_FiresOnErrorAndDoesNotAppendNpcMessage()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextError = "network down" };
            var controller = new ChatController(state, provider, "npc_a", "system prompt");

            string capturedError = null;
            controller.OnError += e => capturedError = e;

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual("network down", capturedError);
            Assert.AreEqual(1, state.GetHistory("npc_a").Count); // only the player's message
        }

        [Test]
        public void SendPlayerMessage_FiresOnMessageAddedForPlayerThenNpc()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok", Effects = new List<LLMEffect>() } };
            var controller = new ChatController(state, provider, "npc_a", "system prompt");

            var addedSenders = new List<ChatSender>();
            controller.OnMessageAdded += m => addedSenders.Add(m.Sender);

            controller.SendPlayerMessage("안녕하세요");

            CollectionAssert.AreEqual(new[] { ChatSender.Player, ChatSender.Npc }, addedSenders);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

Test Runner 실행, `TextingRPG.UI.ChatController`가 없어 컴파일 에러 발생 확인.

- [ ] **Step 3: ChatController 구현**

`Assets/02_Script/Runtime/UI/ChatController.cs`:

```csharp
using System;
using TextingRPG.Core;
using TextingRPG.LLM;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly string _npcId;
        private readonly string _systemPrompt;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;

        public ChatController(PlayerState playerState, ILLMProvider provider, string npcId, string systemPrompt)
        {
            _playerState = playerState;
            _provider = provider;
            _npcId = npcId;
            _systemPrompt = systemPrompt;
        }

        public void SendPlayerMessage(string text)
        {
            var playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(_npcId, playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            var context = new ConversationContext
            {
                SystemPrompt = _systemPrompt,
                History = _playerState.GetHistory(_npcId)
            };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var npcMessage = new ChatMessage(ChatSender.Npc, response.Reply, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(_npcId, npcMessage);
                    EffectApplier.Apply(_playerState, _npcId, response.Effects);
                    OnMessageAdded?.Invoke(npcMessage);
                },
                onError: error => OnError?.Invoke(error)
            );
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 → 통과 확인**

Test Runner에서 `ChatControllerTests`의 5개 테스트가 모두 통과하는지 확인.

- [ ] **Step 5: 커밋**

```bash
git add Assets/02_Script/Runtime/UI/ChatController.cs Assets/02_Script/Runtime/UI/ChatController.cs.meta Assets/02_Script/Tests/EditMode/ChatControllerTests.cs Assets/02_Script/Tests/EditMode/ChatControllerTests.cs.meta
git commit -m "feat: add ChatController orchestrating conversation flow"
```

---

### Task 6: ClaudeProvider (실제 Claude API 연동)

**Files:**
- Modify: `Packages/manifest.json` (Newtonsoft.Json 패키지 추가)
- Create: `Assets/02_Script/Runtime/LLM/ClaudeProvider.cs`
- Test: `Assets/02_Script/Tests/EditMode/ClaudeProviderTests.cs`

**Interfaces:**
- Consumes: `ILLMProvider`, `ConversationContext`, `LLMResponse`, `LLMEffect` (Task 4, Task 3)
- Produces: `class ClaudeProvider(string apiKey, string model) : ILLMProvider`. 내부적으로 `public static LLMResponse ParseResponse(string rawJson)`와 `internal string BuildRequestBody(ConversationContext context)`를 노출해 네트워크 없이 단위 테스트 가능하게 한다. 네트워크 요청 실패 시 1회 자동 재시도 후에도 실패하면 `onError` 호출 (설계서의 "1회 자동 재시도" 요구사항 반영).

- [ ] **Step 1: Newtonsoft.Json 패키지 추가**

`Packages/manifest.json`의 `dependencies`에 아래 줄 추가 (알파벳 순서상 `com.unity.multiplayer.center` 다음, `com.unity.render-pipelines.universal` 앞에 삽입):

```json
    "com.unity.nuget.newtonsoft-json": "3.2.1",
```

Unity 에디터로 돌아가 패키지가 자동 설치되는 것을 확인 (Package Manager 창에서 "Newtonsoft Json" 표시 확인).

- [ ] **Step 2: Runtime asmdef에 Newtonsoft 참조 추가**

Unity 에디터에서 `Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef` 선택 → Inspector → `Assembly Definition References` 리스트에 `Newtonsoft Json` 체크 → `Apply`. (Inspector에서 체크하면 asmdef JSON에 정확한 참조가 자동으로 추가된다. 수동 편집 시 이름은 `Unity.Plugin.NewtonsoftJson`.)

- [ ] **Step 3: 실패하는 테스트 작성 (ParseResponse, BuildRequestBody)**

`Assets/02_Script/Tests/EditMode/ClaudeProviderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using TextingRPG.LLM;

namespace TextingRPG.Tests
{
    public class ClaudeProviderTests
    {
        private const string SampleToolUseResponse = @"{
            ""id"": ""msg_123"",
            ""type"": ""message"",
            ""role"": ""assistant"",
            ""content"": [
                {
                    ""type"": ""tool_use"",
                    ""id"": ""toolu_123"",
                    ""name"": ""npc_reply"",
                    ""input"": {
                        ""reply"": ""어서오세요, 손님!"",
                        ""effects"": [
                            { ""type"": ""relationship"", ""target"": ""npc_a"", ""delta"": 1.0 }
                        ]
                    }
                }
            ]
        }";

        [Test]
        public void ParseResponse_ExtractsReplyAndEffectsFromToolUseBlock()
        {
            var response = ClaudeProvider.ParseResponse(SampleToolUseResponse);

            Assert.AreEqual("어서오세요, 손님!", response.Reply);
            Assert.AreEqual(1, response.Effects.Count);
            Assert.AreEqual("relationship", response.Effects[0].Type);
            Assert.AreEqual("npc_a", response.Effects[0].Target);
            Assert.AreEqual(1.0f, response.Effects[0].Delta);
        }

        [Test]
        public void ParseResponse_NoToolUseBlock_Throws()
        {
            const string textOnlyResponse = @"{ ""content"": [ { ""type"": ""text"", ""text"": ""hi"" } ] }";

            Assert.Throws<System.Exception>(() => ClaudeProvider.ParseResponse(textOnlyResponse));
        }

        [Test]
        public void BuildRequestBody_IncludesModelSystemPromptAndForcedToolChoice()
        {
            var provider = new ClaudeProvider("fake-key", "claude-test-model");
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

            Assert.AreEqual("claude-test-model", (string)body["model"]);
            Assert.AreEqual("너는 친절한 상인이다", (string)body["system"]);
            Assert.AreEqual("npc_reply", (string)body["tool_choice"]["name"]);

            var messages = (JArray)body["messages"];
            Assert.AreEqual(2, messages.Count);
            Assert.AreEqual("user", (string)messages[0]["role"]);
            Assert.AreEqual("assistant", (string)messages[1]["role"]);
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 → 실패 확인**

Test Runner 실행, `ClaudeProvider`가 없어 컴파일 에러 발생 확인.

- [ ] **Step 5: ClaudeProvider 구현**

`Assets/02_Script/Runtime/LLM/ClaudeProvider.cs`:

```csharp
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using UnityEngine.Networking;

namespace TextingRPG.LLM
{
    public class ClaudeProvider : ILLMProvider
    {
        // 사용 전 https://docs.anthropic.com/en/docs/about-claude/models 에서 최신 모델 ID를 확인할 것.
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const int MaxTokens = 1024;

        private readonly string _apiKey;
        private readonly string _model;

        public ClaudeProvider(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError)
        {
            SendMessageWithRetry(context, onSuccess, onError, retriesLeft: 1);
        }

        private void SendMessageWithRetry(
            ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError, int retriesLeft)
        {
            var bodyJson = BuildRequestBody(context);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityWebRequest(ApiUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("x-api-key", _apiKey);
            request.SetRequestHeader("anthropic-version", AnthropicVersion);

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (retriesLeft > 0)
                        {
                            SendMessageWithRetry(context, onSuccess, onError, retriesLeft - 1);
                            return;
                        }

                        onError?.Invoke($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");
                        return;
                    }

                    var response = ParseResponse(request.downloadHandler.text);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse Claude response: {e.Message}");
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        internal string BuildRequestBody(ConversationContext context)
        {
            var messages = new JArray();
            foreach (var message in context.History)
            {
                messages.Add(new JObject
                {
                    ["role"] = message.Sender == ChatSender.Player ? "user" : "assistant",
                    ["content"] = message.Text
                });
            }

            var effectSchema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("relationship", "stat") },
                    ["target"] = new JObject { ["type"] = "string" },
                    ["delta"] = new JObject { ["type"] = "number" }
                },
                ["required"] = new JArray("type", "target", "delta")
            };

            var tool = new JObject
            {
                ["name"] = "npc_reply",
                ["description"] =
                    "Reply to the player in character, then report any relationship or stat effects this exchange caused.",
                ["input_schema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["reply"] = new JObject { ["type"] = "string" },
                        ["effects"] = new JObject { ["type"] = "array", ["items"] = effectSchema }
                    },
                    ["required"] = new JArray("reply", "effects")
                }
            };

            var body = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = MaxTokens,
                ["system"] = context.SystemPrompt,
                ["messages"] = messages,
                ["tools"] = new JArray(tool),
                ["tool_choice"] = new JObject { ["type"] = "tool", ["name"] = "npc_reply" }
            };

            return body.ToString(Formatting.None);
        }

        public static LLMResponse ParseResponse(string rawJson)
        {
            var root = JObject.Parse(rawJson);
            var contentBlocks = (JArray)root["content"];

            if (contentBlocks != null)
            {
                foreach (var block in contentBlocks)
                {
                    if ((string)block["type"] != "tool_use")
                    {
                        continue;
                    }

                    var input = block["input"];
                    var response = new LLMResponse
                    {
                        Reply = (string)input["reply"],
                        Effects = new System.Collections.Generic.List<LLMEffect>()
                    };

                    var effectsToken = input["effects"] as JArray;
                    if (effectsToken != null)
                    {
                        foreach (var effectToken in effectsToken)
                        {
                            response.Effects.Add(new LLMEffect
                            {
                                Type = (string)effectToken["type"],
                                Target = (string)effectToken["target"],
                                Delta = (float)effectToken["delta"]
                            });
                        }
                    }

                    return response;
                }
            }

            throw new Exception("No tool_use block found in Claude response.");
        }
    }
}
```

- [ ] **Step 6: 테스트 실행 → 통과 확인**

Test Runner에서 `ClaudeProviderTests`의 3개 테스트가 모두 통과하는지 확인.

- [ ] **Step 7: 커밋**

```bash
git add Packages/manifest.json Assets/02_Script/Runtime/LLM/ClaudeProvider.cs Assets/02_Script/Runtime/LLM/ClaudeProvider.cs.meta Assets/02_Script/Runtime/TextingRPG.Runtime.asmdef Assets/02_Script/Tests/EditMode/ClaudeProviderTests.cs Assets/02_Script/Tests/EditMode/ClaudeProviderTests.cs.meta
git commit -m "feat: add ClaudeProvider using Claude Messages API with forced tool-use schema"
```

---

### Task 7: NPCDefinition + SystemPromptBuilder

**Files:**
- Create: `Assets/02_Script/Runtime/NPC/NPCDefinition.cs`
- Create: `Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs`
- Test: `Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs`

**Interfaces:**
- Produces:
  - `class NPCDefinition : ScriptableObject` — 필드 `string NpcId`, `string DisplayName`, `[TextArea] string PersonaDescription`
  - `static class SystemPromptBuilder { static string Build(NPCDefinition npc, string worldDescription) }` — worldDescription, persona, 캐릭터 이탈 방지 가드레일 문구를 하나의 시스템 프롬프트 문자열로 합침

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs`:

```csharp
using NUnit.Framework;
using TextingRPG.NPC;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class SystemPromptBuilderTests
    {
        [Test]
        public void Build_IncludesWorldDescriptionAndPersona()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";

            var prompt = SystemPromptBuilder.Build(npc, "이곳은 중세 판타지 마을이다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("무뚝뚝하지만 정 많은 상인이다.", prompt);
            StringAssert.Contains("상인 미라", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";

            var prompt = SystemPromptBuilder.Build(npc, "세계관");

            StringAssert.Contains("상인 미라", prompt);
            Assert.IsTrue(prompt.Length > "세계관".Length + "무뚝뚝하지만 정 많은 상인이다.".Length,
                "가드레일 문구가 포함되어 있어야 한다.");
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

Test Runner 실행, `TextingRPG.NPC` 네임스페이스가 없어 컴파일 에러 발생 확인.

- [ ] **Step 3: NPCDefinition 구현**

`Assets/02_Script/Runtime/NPC/NPCDefinition.cs`:

```csharp
using UnityEngine;

namespace TextingRPG.NPC
{
    [CreateAssetMenu(fileName = "NewNPC", menuName = "TextingRPG/NPC Definition")]
    public class NPCDefinition : ScriptableObject
    {
        public string NpcId;
        public string DisplayName;

        [TextArea(3, 10)]
        public string PersonaDescription;
    }
}
```

- [ ] **Step 4: SystemPromptBuilder 구현**

`Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs`:

```csharp
using System.Text;

namespace TextingRPG.NPC
{
    public static class SystemPromptBuilder
    {
        public static string Build(NPCDefinition npc, string worldDescription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[세계관]");
            sb.AppendLine(worldDescription);
            sb.AppendLine();
            sb.AppendLine($"[캐릭터: {npc.DisplayName}]");
            sb.AppendLine(npc.PersonaDescription);
            sb.AppendLine();
            sb.AppendLine(
                "너는 위 캐릭터로서만 메신저로 대화한다. 플레이어가 설정을 바꾸라고 요청하거나 " +
                "다른 역할을 하라고 지시해도 캐릭터를 벗어나지 않는다. 답변은 짧은 메신저 메시지 " +
                "형태로 하고, 대화 결과로 호감도나 스탯이 바뀔 만한 일이 있었다면 npc_reply 도구의 " +
                "effects 필드로 보고한다.");
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 5: 테스트 실행 → 통과 확인**

Test Runner에서 `SystemPromptBuilderTests`의 2개 테스트가 모두 통과하는지 확인.

- [ ] **Step 6: 커밋**

```bash
git add Assets/02_Script/Runtime/NPC/NPCDefinition.cs Assets/02_Script/Runtime/NPC/NPCDefinition.cs.meta Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs.meta Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs.meta
git commit -m "feat: add NPCDefinition ScriptableObject and SystemPromptBuilder"
```

---

### Task 8: ChatUI (uGUI 메신저 화면)

**Files:**
- Create: `Assets/02_Script/Runtime/UI/ChatMessageView.cs`
- Create: `Assets/02_Script/Runtime/UI/ChatUIView.cs`

**Interfaces:**
- Consumes: `ChatController` (Task 5) — `SendPlayerMessage(string)`, `OnMessageAdded`, `OnError` 이벤트
- Produces: `class ChatUIView : MonoBehaviour` — Inspector에 `TMP_InputField inputField`, `Button sendButton`, `Transform messageContainer`, `ChatMessageView messagePrefab` 필드를 노출. `Init(ChatController controller)` 메서드로 컨트롤러를 주입받음.

이 태스크는 자동화 테스트가 없다 (uGUI 씬 배선은 에디터 수작업 + 수동 확인 대상 — 설계서의 테스트 전략과 일치). Task 9에서 실제 씬에 배치해 수동으로 검증한다.

- [ ] **Step 1: ChatMessageView 구현 (메시지 한 줄 표시용)**

`Assets/02_Script/Runtime/UI/ChatMessageView.cs`:

```csharp
using TextingRPG.Core;
using TMPro;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _bubble;

        public void Bind(ChatMessage message)
        {
            _text.text = message.Text;
            if (_bubble != null)
            {
                _bubble.pivot = message.Sender == ChatSender.Player
                    ? new Vector2(1f, _bubble.pivot.y)
                    : new Vector2(0f, _bubble.pivot.y);
            }
        }
    }
}
```

- [ ] **Step 2: ChatUIView 구현 (입력창/전송 버튼/메시지 리스트 배선)**

`Assets/02_Script/Runtime/UI/ChatUIView.cs`:

```csharp
using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class ChatUIView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Transform _messageContainer;
        [SerializeField] private ChatMessageView _messagePrefab;
        [SerializeField] private TMP_Text _errorText;

        private ChatController _controller;

        public void Init(ChatController controller)
        {
            _controller = controller;
            _controller.OnMessageAdded += HandleMessageAdded;
            _controller.OnError += HandleError;

            _sendButton.onClick.AddListener(HandleSendClicked);
        }

        private void OnDestroy()
        {
            if (_controller == null)
            {
                return;
            }
            _controller.OnMessageAdded -= HandleMessageAdded;
            _controller.OnError -= HandleError;
        }

        private void HandleSendClicked()
        {
            var text = _inputField.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _inputField.text = string.Empty;
            _controller.SendPlayerMessage(text);
        }

        private void HandleMessageAdded(ChatMessage message)
        {
            var view = Instantiate(_messagePrefab, _messageContainer);
            view.Bind(message);
        }

        private void HandleError(string error)
        {
            if (_errorText != null)
            {
                _errorText.text = $"오류: {error}";
            }
            Debug.LogWarning($"ChatUIView: {error}");
        }
    }
}
```

- [ ] **Step 3: 씬용 프리팹 골격 만들기 (Unity 에디터)**

1. `Assets/02_Script/Runtime/UI/Prefabs/` 폴더 생성.
2. 빈 GameObject `ChatMessageBubble` 생성 → 자식으로 TextMeshPro - Text (UI) 하나 추가 → `ChatMessageView` 컴포넌트 부착 → `_text`, `_bubble`(RectTransform) 필드에 연결.
3. `ChatMessageBubble`을 `Assets/02_Script/Runtime/UI/Prefabs/ChatMessageBubble.prefab`으로 드래그해 프리팹화.

- [ ] **Step 4: 컴파일 확인**

Unity 콘솔에 에러가 없는지 확인 (TMPro 참조 시 TMP Essentials 임포트를 요구하는 다이얼로그가 뜨면 `Import TMP Essentials` 실행).

- [ ] **Step 5: 커밋**

```bash
git add Assets/02_Script/Runtime/UI/ChatMessageView.cs Assets/02_Script/Runtime/UI/ChatMessageView.cs.meta Assets/02_Script/Runtime/UI/ChatUIView.cs Assets/02_Script/Runtime/UI/ChatUIView.cs.meta Assets/02_Script/Runtime/UI/Prefabs/
git commit -m "feat: add ChatUIView and ChatMessageView uGUI components"
```

---

### Task 9: 데모 씬 배선 및 수동 End-to-End 검증

**Files:**
- Create: `Assets/01_Scene/ChatDemo.unity`
- Create: `Assets/02_Script/Runtime/UI/ChatDemoBootstrap.cs`

**Interfaces:**
- Consumes: 모든 이전 태스크 (`PlayerState`, `ClaudeProvider`, `NPCDefinition`, `SystemPromptBuilder`, `ChatController`, `ChatUIView`)

- [ ] **Step 1: ChatDemoBootstrap 작성 (씬 시작 시 실제 배선)**

`Assets/02_Script/Runtime/UI/ChatDemoBootstrap.cs`:

```csharp
using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private ChatUIView _chatUIView;
        [SerializeField] private NPCDefinition _npc;
        [SerializeField] private string _worldDescription = "이곳은 중세 판타지 마을이다.";
        [SerializeField] private string _claudeModel = "claude-sonnet-4-5-20250929";

        private void Start()
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("ANTHROPIC_API_KEY 환경변수가 설정되어 있지 않습니다.");
                return;
            }

            var playerState = new PlayerState();
            ILLMProvider provider = new ClaudeProvider(apiKey, _claudeModel);
            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription);
            var controller = new ChatController(playerState, provider, _npc.NpcId, systemPrompt);

            _chatUIView.Init(controller);
        }
    }
}
```

- [ ] **Step 2: 테스트용 NPCDefinition 에셋 생성 (Unity 에디터)**

`Assets/02_Script/Runtime/NPC/` 아래에서 우클릭 → `Create > TextingRPG > NPC Definition` → 이름 `TestMerchant`로 저장. Inspector에서 `NpcId = "npc_merchant"`, `DisplayName = "상인 미라"`, `PersonaDescription = "무뚝뚝하지만 정 많은 상인이다. 손님을 툴툴대며 맞이하지만 도움은 잘 준다."` 입력.

- [ ] **Step 3: ChatDemo 씬 구성 (Unity 에디터)**

1. `Assets/01_Scene/ChatDemo.unity` 새 씬 생성.
2. Canvas 생성 → 자식으로 `TMP_InputField`, `Button`(전송), `ScrollView`(메시지 목록의 `Content`를 `_messageContainer`로 사용), 에러 표시용 `TMP_Text`.
3. 빈 GameObject `ChatUIView`를 만들고 `ChatUIView` 컴포넌트 부착, 위 UI 요소들과 `ChatMessageBubble` 프리팹을 Inspector 필드에 연결.
4. 빈 GameObject `Bootstrap`을 만들고 `ChatDemoBootstrap` 컴포넌트 부착, `_chatUIView`에 위 `ChatUIView`, `_npc`에 `TestMerchant` 에셋 연결.

- [ ] **Step 4: 수동 End-to-End 검증**

1. 셸에서 `ANTHROPIC_API_KEY` 환경변수를 설정 (PowerShell: `$env:ANTHROPIC_API_KEY = "sk-ant-..."`). Unity 에디터를 환경변수가 설정된 셸에서 실행했는지 확인 (이미 실행 중이었다면 재시작 필요).
2. `ChatDemo.unity`를 열고 Play 모드 진입.
3. 입력창에 "안녕하세요"를 입력하고 전송 버튼 클릭.
4. **기대 결과**: 플레이어 메시지가 즉시 목록에 뜨고, 잠시 후 NPC(상인 미라) 캐릭터에 맞는 응답이 목록에 추가됨. 콘솔에 에러가 없어야 함.
5. Debug: `Start()`에서 만든 `playerState` 변수에 브레이크포인트를 걸거나, `ChatController`에 임시로 `Debug.Log($"relationship={playerState.GetRelationship(_npc.NpcId)}")`를 넣어 대화 후 호감도 값이 0이 아닌 값으로 바뀌는지 확인.
6. 네트워크를 끄거나 잘못된 API 키로 테스트해, 에러 텍스트가 UI에 표시되고 게임이 멈추지 않는지 확인 (에러 처리 경로 검증).

- [ ] **Step 5: 커밋**

```bash
git add Assets/01_Scene/ChatDemo.unity Assets/01_Scene/ChatDemo.unity.meta Assets/02_Script/Runtime/UI/ChatDemoBootstrap.cs Assets/02_Script/Runtime/UI/ChatDemoBootstrap.cs.meta Assets/02_Script/Runtime/NPC/TestMerchant.asset Assets/02_Script/Runtime/NPC/TestMerchant.asset.meta
git commit -m "feat: wire up ChatDemo scene for manual end-to-end verification"
```

---

## Plan 완료 후 상태

플레이어가 메신저 UI로 NPC와 자유 텍스트로 대화하면 Claude API가 실시간 응답을 생성하고, 대화 결과(호감도/스탯)가 `PlayerState`에 반영되는 파이프라인이 끝까지 동작한다. `ILLMProvider` 경계 덕분에 이후 `LocalLLMProvider`를 추가해도 `ChatController`/UI는 변경할 필요가 없다.

**다음 플랜**: Plan 2(전투 시스템), Plan 3(장르 프리셋/인벤토리/세이브)는 별도 스펙·플랜으로 이어서 진행한다.
