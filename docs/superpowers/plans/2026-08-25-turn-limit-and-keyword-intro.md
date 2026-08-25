# 100턴 이야기 마무리 + 시작 전 키워드 선택 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 플레이어 전송 80턴부터 시스템 프롬프트로 마무리를 유도하고 100턴째는 LLM이 그 자리에서 결말을 자유롭게 지어내게 한 뒤 입력을 잠근다. 또한 채팅이 시작되기 전에 미리 준비된 키워드 6개 중 3개를 고르는 화면을 추가해, 고른 키워드가 세계관 설명에 반영되게 한다.

**Architecture:** `PlayerState`에 NPC별 턴 카운트를 추가하고, `ChatController`가 이 카운트를 기준으로 `SystemPromptBuilder`에 넘길 힌트 문자열을 결정한다(`SystemPromptBuilder`는 턴 숫자를 모른다). 100턴째엔 `ChatController`가 `OnConversationEnded` 이벤트를 발행하고 이후 `SendPlayerMessage` 호출을 무시한다. 키워드 선택은 새 `KeywordIntroPanel` 컴포넌트가 담당하고, `ChatBootstrap`은 키워드가 확정된 뒤에야 `ChatController`를 생성하도록 재구성한다.

**Tech Stack:** Unity 6000.3.10f1, C# / Unity Test Framework(NUnit, EditMode), Gemini API.

## Global Constraints

- 고정된 결말 콘텐츠는 미리 작성하지 않는다 — 결말은 매 플레이마다 LLM이 대화 흐름을 바탕으로 자유롭게 생성한다.
- 턴당 API 호출은 여전히 1회 — 마무리 힌트는 기존 스키마(`narration`/`npcLine`/`tags`/`effects`)를 그대로 쓰고, 시스템 프롬프트에 문단 하나만 추가한다.
- 키워드 목록은 코드에 배열로 두고, 나중에 항목을 추가하면 화면에 자동으로 버튼이 늘어나게 한다 (버튼을 씬에 하나씩 미리 배치하지 않는다).
- 테스트 실행은 Unity MCP의 `run_tests` 도구(`mode: "EditMode"`, `test_names`로 필터 가능)를 사용한다.

---

## Task 1: `PlayerState`에 NPC별 턴 카운트 추가

**Files:**
- Modify: `Assets/02_Script/Runtime/Core/PlayerState.cs`
- Modify: `Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`

**Interfaces:**
- Produces: `PlayerState.GetTurnCount(string npcId) : int` (기본값 0), `PlayerState.IncrementTurnCount(string npcId) : void` — Task 3(`ChatController`)이 이 둘을 소비한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`PlayerStateTests.cs`의 마지막 테스트(`SetStoryNode_ThenGet_ReturnsSetValue`) 뒤, 클래스 닫는 `}` 앞에 추가한다:

```csharp
        [Test]
        public void GetTurnCount_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetTurnCount("npc_a"));
        }

        [Test]
        public void IncrementTurnCount_IncreasesCountForThatNpc()
        {
            var state = new PlayerState();
            state.IncrementTurnCount("npc_a");
            state.IncrementTurnCount("npc_a");
            Assert.AreEqual(2, state.GetTurnCount("npc_a"));
        }

        [Test]
        public void IncrementTurnCount_DoesNotAffectOtherNpcsCount()
        {
            var state = new PlayerState();
            state.IncrementTurnCount("npc_a");
            Assert.AreEqual(0, state.GetTurnCount("npc_b"));
        }
```

- [ ] **Step 2: 테스트 실행 → 컴파일 실패 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.PlayerStateTests"]`)를 실행한다.
Expected: 컴파일 실패 (`PlayerState`에 `GetTurnCount`/`IncrementTurnCount`가 없음).

- [ ] **Step 3: `PlayerState.cs`에 턴 카운트 필드/메서드 추가**

`_storyNodes` 필드 선언 바로 아래에 추가한다:

```csharp
        private readonly Dictionary<string, int> _turnCounts = new Dictionary<string, int>();
```

`SetStoryNode` 메서드 뒤, 클래스 닫는 `}` 앞에 추가한다:

```csharp

        public int GetTurnCount(string npcId) =>
            _turnCounts.TryGetValue(npcId, out var value) ? value : 0;

        public void IncrementTurnCount(string npcId) =>
            _turnCounts[npcId] = GetTurnCount(npcId) + 1;
```

- [ ] **Step 4: 테스트 실행 → 통과 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.PlayerStateTests"]`)를 실행한다.
Expected: PASS (9개 테스트 전부).

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/Core/PlayerState.cs Assets/02_Script/Tests/EditMode/PlayerStateTests.cs
git commit -m "feat: track per-NPC turn count in PlayerState"
```

---

## Task 2: `SystemPromptBuilder`에 선택적 `endingHint` 파라미터 추가

**Files:**
- Modify: `Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs`
- Modify: `Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs`

**Interfaces:**
- Produces: `SystemPromptBuilder.Build(NPCDefinition npc, string worldDescription, StoryNode currentNode, string endingHint = "")` — 기존 3-인자 호출부는 그대로 컴파일된다. Task 3(`ChatController`)이 4번째 인자로 마무리 힌트를 넘긴다.

- [ ] **Step 1: 실패하는 테스트 작성**

`SystemPromptBuilderTests.cs`의 마지막 테스트(`Build_IncludesNarrationAndNpcLineInstruction`) 뒤, 클래스 닫는 `}` 앞에 추가한다:

```csharp

        [Test]
        public void Build_WithEndingHint_AppendsHintToPrompt()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode(), "이번이 마지막 턴이다.");

            StringAssert.Contains("이번이 마지막 턴이다.", prompt);
        }

        [Test]
        public void Build_WithoutEndingHint_SameAsEmptyStringHint()
        {
            var promptWithoutArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode());
            var promptWithEmptyArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode(), "");

            Assert.AreEqual(promptWithoutArg, promptWithEmptyArg);
        }
```

- [ ] **Step 2: 테스트 실행 → 컴파일 실패 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.SystemPromptBuilderTests"]`)를 실행한다.
Expected: 컴파일 실패 (`Build`가 4번째 인자를 받지 않음).

- [ ] **Step 3: `SystemPromptBuilder.cs` 수정**

`Build` 메서드 시그니처를 교체한다:

```csharp
        public static string Build(NPCDefinition npc, string worldDescription, StoryNode currentNode, string endingHint = "")
```

`return sb.ToString();` 바로 앞에 추가한다:

```csharp
            if (!string.IsNullOrEmpty(endingHint))
            {
                sb.AppendLine();
                sb.AppendLine(endingHint);
            }

```

- [ ] **Step 4: 테스트 실행 → 통과 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.SystemPromptBuilderTests"]`)를 실행한다.
Expected: PASS (6개 테스트 전부).

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/NPC/SystemPromptBuilder.cs Assets/02_Script/Tests/EditMode/SystemPromptBuilderTests.cs
git commit -m "feat: add optional endingHint paragraph to SystemPromptBuilder"
```

---

## Task 3: `ChatController`가 80턴부터 마무리 유도, 100턴에 대화 종료

**Files:**
- Modify: `Assets/02_Script/Runtime/UI/ChatController.cs`
- Modify: `Assets/02_Script/Tests/EditMode/ChatControllerTests.cs`

**Interfaces:**
- Consumes: `PlayerState.GetTurnCount`/`IncrementTurnCount` (Task 1), `SystemPromptBuilder.Build(..., endingHint)` (Task 2)
- Produces: `ChatController.OnConversationEnded : event Action` — Task 5(`ChatBootstrap`)이 구독해서 입력을 잠근다. 100턴째 이후 `SendPlayerMessage` 호출은 아무 효과가 없다(가드).

- [ ] **Step 1: 실패하는 테스트 작성**

`ChatControllerTests.cs`의 마지막 테스트(`SendPlayerMessage_OnSuccess_NpcLinePresent_AddsNarrationThenNpcMessage`) 뒤, 클래스 닫는 `}` 앞에 추가한다:

```csharp

        [Test]
        public void SendPlayerMessage_BeforeWrapUpThreshold_SystemPromptHasNoEndingHint()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

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
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

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
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

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
        public void SendPlayerMessage_AfterConversationEnded_DoesNothing()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Narration = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            for (int i = 0; i < 100; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            int historyCountAtEnd = state.GetHistory("npc_a").Count;

            controller.SendPlayerMessage("한 번 더");

            Assert.AreEqual(historyCountAtEnd, state.GetHistory("npc_a").Count);
        }
```

- [ ] **Step 2: 테스트 실행 → 실패 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.ChatControllerTests"]`)를 실행한다.
Expected: 컴파일은 정상이지만(기존 시그니처 그대로 호출), 새 4개 테스트는 FAIL한다 — `OnConversationEnded`가 아직 없어 컴파일 자체가 깨질 수도 있다(이 경우 컴파일 실패로 나타남). `StringAssert.Contains` 관련 테스트들은 힌트 로직이 없어 실패한다.

- [ ] **Step 3: `ChatController.cs` 수정**

파일 전체를 아래로 교체한다:

```csharp
using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 80;
        private const int MaxTurns = 100;

        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly NPCDefinition _npc;
        private readonly string _worldDescription;
        private readonly StoryGraph _storyGraph;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action<string> OnStoryNodeChanged;
        public event Action OnConversationEnded;

        public string NpcDisplayName => _npc.DisplayName;

        public ChatController(
            PlayerState playerState, ILLMProvider provider, NPCDefinition npc,
            string worldDescription, StoryGraph storyGraph)
        {
            _playerState = playerState;
            _provider = provider;
            _npc = npc;
            _worldDescription = worldDescription;
            _storyGraph = storyGraph;
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded) return;

            ChatMessage playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(_npc.NpcId, playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            _playerState.IncrementTurnCount(_npc.NpcId);
            int turnCount = _playerState.GetTurnCount(_npc.NpcId);
            bool isFinalTurn = turnCount >= MaxTurns;

            var currentNodeId = _playerState.GetStoryNode(_npc.NpcId) ?? _storyGraph.StartNodeId;
            _playerState.SetStoryNode(_npc.NpcId, currentNodeId);
            var currentNode = _storyGraph.GetNode(currentNodeId);

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription, currentNode, endingHint);
            var recentHistory = HistoryWindow.TakeRecent(_playerState.GetHistory(_npc.NpcId), MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(_npc.NpcId, narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(_npc.NpcId, npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    EffectApplier.Apply(_playerState, _npc.NpcId, response.Effects);

                    var nextNodeId = StoryProgression.Resolve(currentNode, response.Tags, out _);
                    if (nextNodeId != currentNodeId)
                    {
                        _playerState.SetStoryNode(_npc.NpcId, nextNodeId);
                        OnStoryNodeChanged?.Invoke(nextNodeId);
                    }

                    if (isFinalTurn)
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

- [ ] **Step 4: 테스트 실행 → 통과 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.ChatControllerTests"]`)를 실행한다.
Expected: PASS (13개 테스트 전부).

- [ ] **Step 5: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (전부).

- [ ] **Step 6: Commit**

```bash
git add Assets/02_Script/Runtime/UI/ChatController.cs Assets/02_Script/Tests/EditMode/ChatControllerTests.cs
git commit -m "feat: ChatController nudges wrap-up at turn 80, forces freeform ending and locks at turn 100"
```

---

## Task 4: `KeywordIntroPanel` 컴포넌트 작성

**Files:**
- Create: `Assets/02_Script/Runtime/UI/KeywordIntroPanel.cs`

**Interfaces:**
- Produces: `KeywordIntroPanel.OnKeywordsConfirmed : event Action<string[]>` — Task 5(`ChatBootstrap`)이 구독해서 키워드를 받는다. `[SerializeField]` 필드 `availableKeywords`(string[]), `keywordButtonContainer`(Transform), `keywordButtonPrefab`(Button), `startButton`(Button), `requiredSelectionCount`(int, 기본 3) — Task 5에서 씬 인스펙터로 할당한다.
- 이 컴포넌트는 UI 상호작용 위주라 EditMode 자동 테스트를 두지 않는다(프로젝트의 `ChatBubbleListView`와 같은 이유) — Task 5의 수동 Play 모드 검증에서 함께 확인한다.

- [ ] **Step 1: `KeywordIntroPanel.cs` 작성**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class KeywordIntroPanel : MonoBehaviour
    {
        [SerializeField] string[] availableKeywords =
            { "안개", "폭풍우", "낡은 지도", "은화", "늑대 울음", "별빛" };
        [SerializeField] Transform keywordButtonContainer;
        [SerializeField] Button keywordButtonPrefab;
        [SerializeField] Button startButton;
        [SerializeField] int requiredSelectionCount = 3;

        public event Action<string[]> OnKeywordsConfirmed;

        private readonly List<string> _selected = new List<string>();

        private void Awake()
        {
            foreach (var keyword in availableKeywords)
            {
                var button = Instantiate(keywordButtonPrefab, keywordButtonContainer);
                button.GetComponentInChildren<TMP_Text>().text = keyword;
                button.onClick.AddListener(() => ToggleKeyword(keyword, button));
            }

            startButton.onClick.AddListener(ConfirmSelection);
            startButton.interactable = false;
        }

        private void ToggleKeyword(string keyword, Button button)
        {
            if (_selected.Contains(keyword))
            {
                _selected.Remove(keyword);
                SetButtonSelectedVisual(button, false);
            }
            else if (_selected.Count < requiredSelectionCount)
            {
                _selected.Add(keyword);
                SetButtonSelectedVisual(button, true);
            }

            startButton.interactable = _selected.Count == requiredSelectionCount;
        }

        private static void SetButtonSelectedVisual(Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.75f, 0.85f, 1f) : Color.white;
            button.colors = colors;
        }

        private void ConfirmSelection()
        {
            OnKeywordsConfirmed?.Invoke(_selected.ToArray());
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행해 프로젝트 전체가 컴파일되는지 확인한다 (이 컴포넌트 자체를 검증하는 테스트는 아니다).
Expected: PASS (기존 테스트 전부, 새로 추가된 테스트는 없음).

- [ ] **Step 3: Commit**

```bash
git add Assets/02_Script/Runtime/UI/KeywordIntroPanel.cs
git commit -m "feat: add KeywordIntroPanel for pre-game keyword selection"
```

---

## Task 5: 씬 구성 + `ChatBootstrap` 재배선 + 수동 End-to-End 검증

**Files:**
- Create: `Assets/04_Prefab/KeywordButton.prefab`
- Modify: `Assets/02_Script/Runtime/UI/ChatBootstrap.cs`
- Modify: 씬 `Assets/01_Scene/PlayScene.unity`

**Interfaces:**
- Consumes: `KeywordIntroPanel.OnKeywordsConfirmed` (Task 4), `ChatController.OnConversationEnded` (Task 3)
- 이 태스크로 플랜의 마지막 산출물이 나온다 — 자동화 테스트 대상 아님, 수동 Play 모드 검증으로 마무리.

- [ ] **Step 1: `ChatBootstrap.cs` 재구성**

파일 전체를 아래로 교체한다:

```csharp
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // ChatController <-> ChatBubbleListView 배선을 실제 Gemini API로 연결하는 부트스트랩.
    // API 키는 프로젝트 루트의 secrets.local.json(커밋 안 됨)에서 읽는다 (코드/에셋에 하드코딩 금지).
    public class ChatBootstrap : MonoBehaviour
    {
        [SerializeField] ChatBubbleListView chatBubbleListView;
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;
        [SerializeField] string geminiModel = "gemini-3.6-flash";
        [SerializeField] float minSecondsBetweenSends = 4f; // Gemini 무료 티어 RPM(분당 15회) 한도에 맞춘 최소 전송 간격
        [SerializeField] NPCDefinition npc;
        [SerializeField] StoryGraph storyGraph;
        [SerializeField] string worldDescription = "중세 판타지 세계, 변방의 작은 마을. 플레이어는 이제 막 마을에 도착한 여행자다.";
        [SerializeField] KeywordIntroPanel keywordIntroPanel;
        [SerializeField] GameObject chatUIRoot;

        private ChatController _controller;
        private float _lastSendTime = float.NegativeInfinity;
        private string _apiKey;

        private void Start()
        {
            _apiKey = LocalSecrets.GetGeminiApiKey();
            if (string.IsNullOrEmpty(_apiKey))
            {
                return;
            }

            chatUIRoot.SetActive(false);
            keywordIntroPanel.OnKeywordsConfirmed += HandleKeywordsConfirmed;
        }

        private void HandleKeywordsConfirmed(string[] keywords)
        {
            var finalWorldDescription = worldDescription + "\n\n[이번 모험의 키워드] " + string.Join(", ", keywords);

            var playerState = new PlayerState();
            ILLMProvider provider = new GeminiProvider(_apiKey, geminiModel);

            _controller = new ChatController(playerState, provider, npc, finalWorldDescription, storyGraph);
            _controller.OnError += HandleError;
            _controller.OnConversationEnded += HandleConversationEnded;
            chatBubbleListView.Bind(_controller);
            chatBubbleListView.OnMessagePlaybackComplete += HandleMessagePlaybackComplete;

            sendButton.onClick.AddListener(SendCurrentInput);
            inputField.onSubmit.AddListener(_ => SendCurrentInput());

            chatUIRoot.SetActive(true);
        }

        private void SendCurrentInput()
        {
            var text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            sendButton.interactable = false;
            _lastSendTime = Time.time;
            _controller.SendPlayerMessage(text);
        }

        private void HandleMessagePlaybackComplete(ChatMessage message)
        {
            if (message.Sender != ChatSender.Player) EnableSendButtonRespectingCooldown();
        }

        private void HandleError(string error)
        {
            Debug.LogError($"Chat error: {error}");
            EnableSendButtonRespectingCooldown();
        }

        private void HandleConversationEnded()
        {
            CancelInvoke(nameof(EnableSendButtonNow));
            sendButton.interactable = false;
            inputField.interactable = false;
        }

        private void EnableSendButtonRespectingCooldown()
        {
            float remaining = minSecondsBetweenSends - (Time.time - _lastSendTime);
            if (remaining <= 0f) sendButton.interactable = true;
            else Invoke(nameof(EnableSendButtonNow), remaining);
        }

        private void EnableSendButtonNow() => sendButton.interactable = true;
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (전체 컴파일 정상, 테스트 수 변화 없음).

- [ ] **Step 3: 키워드 버튼 프리팹 생성 (Unity 에디터 스크립팅)**

`Assets/04_Prefab/KeywordButton.prefab`을 만든다 — `RectTransform`(약 260×100) + `Image`(버튼 배경) + `Button` 컴포넌트 + 자식으로 가운데 정렬된 `TextMeshProUGUI` 라벨(빈 텍스트, 폰트 크기 32 정도)이 있는 오브젝트. `ChatBubble.prefab`을 만들 때처럼 코드로 구성한 뒤 `PrefabUtility.SaveAsPrefabAsset`으로 저장한다.

- [ ] **Step 4: 씬 계층 재구성 (Unity 에디터 스크립팅)**

`Assets/01_Scene/PlayScene.unity`에서:

1. `Canvas/ChatUI` 아래에 `ChatUIRoot`라는 새 자식 오브젝트를 만든다. `RectTransform`을 `ChatUI`와 동일하게 꽉 채우도록(`anchorMin=(0,0)`, `anchorMax=(1,1)`, `offsetMin/Max=0`) 설정한다.
2. 기존 `Scroll View`, `SendButton`, `MessageInputField`를 `ChatUIRoot`의 자식으로 옮긴다(`SetParent(chatUIRoot.transform, worldPositionStays: false)` — 기존 앵커/사이즈 값이 그대로 유지된다).
3. `Canvas/ChatUI` 아래에 `KeywordIntroPanel`이라는 새 자식 오브젝트를 만든다 (역시 꽉 채우는 `RectTransform`). 반투명 배경 `Image`, `GridLayoutGroup`이 붙은 `ButtonContainer` 자식(3열 2행, cell size 260×100, spacing 20,20, 화면 중앙에 배치), "모험 시작" 텍스트가 있는 `StartButton`(하단 중앙 배치)을 만든다. 루트에 `KeywordIntroPanel` 컴포넌트를 붙인다.
4. `KeywordIntroPanel` 컴포넌트의 `keywordButtonContainer`에 `ButtonContainer`, `keywordButtonPrefab`에 Step 3에서 만든 프리팹, `startButton`에 `StartButton`을 할당한다.
5. `ChatBootstrap` 컴포넌트의 `keywordIntroPanel`에 방금 만든 `KeywordIntroPanel`, `chatUIRoot`에 `ChatUIRoot` 오브젝트를 할당한다.
6. 씬을 저장한다.

- [ ] **Step 5: 수동 End-to-End 검증 (Play 모드)**

Play 모드로 진입해서 아래를 순서대로 확인한다:

1. 채팅 UI(스크롤뷰/입력창/전송버튼)는 안 보이고 키워드 선택 화면만 보이는지 확인
2. 키워드를 2개만 고르면 "모험 시작" 버튼이 비활성 상태인지, 3개를 고르면 활성화되는지, 이미 고른 키워드를 다시 눌러 해제할 수 있는지 확인
3. "모험 시작"을 누르면 키워드 화면이 사라지고 채팅 UI가 나타나며, 정상적으로 첫 메시지를 보낼 수 있는지 확인 (실제 Gemini 호출 1회로 확인)
4. Play 모드에서 아래 스크립트를 실행해 턴 카운트를 인위적으로 올려서 마무리 로직을 확인한다 (매번 실제 API를 100번 부르는 건 비용/시간이 크므로):

```csharp
var bootstrap = UnityEngine.Object.FindFirstObjectByType<TextingRPG.UI.ChatBootstrap>();
var controllerField = typeof(TextingRPG.UI.ChatBootstrap).GetField("_controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var controller = controllerField.GetValue(bootstrap);
var playerStateField = typeof(TextingRPG.UI.ChatController).GetField("_playerState", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var playerState = (TextingRPG.Core.PlayerState)playerStateField.GetValue(controller);
for (int i = 0; i < 79; i++) playerState.IncrementTurnCount("innkeeper_gert");
```

이 상태에서 메시지를 하나 보내면(80번째 턴) 나레이션 톤이 마무리 쪽으로 기우는지 확인하고, 다시 `IncrementTurnCount`를 19번 더 호출한 뒤(100번째 턴) 메시지를 보내면 결말이 지어지고 입력창/전송 버튼이 잠기는지 확인한다.

- [ ] **Step 6: Commit**

```bash
git add Assets/04_Prefab/KeywordButton.prefab Assets/04_Prefab/KeywordButton.prefab.meta Assets/02_Script/Runtime/UI/ChatBootstrap.cs Assets/01_Scene/PlayScene.unity
git commit -m "feat: wire KeywordIntroPanel into ChatBootstrap and gate chat UI behind keyword selection"
```

---

## Self-Review Notes

- **스펙 커버리지**: 턴 카운트(Task 1), 프롬프트 힌트(Task 2), 마무리/잠금 로직(Task 3), 키워드 패널 컴포넌트(Task 4), 씬 배선+수동 검증(Task 5) — 스펙의 두 기능 모두 태스크로 매핑된다.
- **타입 일관성**: `PlayerState.GetTurnCount`/`IncrementTurnCount`(Task 1) → `ChatController`(Task 3)에서 그대로 사용. `SystemPromptBuilder.Build`의 4번째 인자명 `endingHint`가 Task 2/3에서 동일하게 쓰인다. `KeywordIntroPanel.OnKeywordsConfirmed`(Task 4) → `ChatBootstrap.HandleKeywordsConfirmed`(Task 5)의 시그니처(`string[]`)가 일치한다.
- **플레이스홀더 스캔**: 없음.
