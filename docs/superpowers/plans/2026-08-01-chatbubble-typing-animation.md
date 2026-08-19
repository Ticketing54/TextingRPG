# ChatBubble Typing Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the `ChatBubble` field-only skeleton into a working component that aligns itself by sender and reveals NPC/Narration text with a DOTween typewriter effect, per `docs/superpowers/specs/2026-08-01-chatbubble-typing-animation-design.md`.

**Architecture:** `ChatSender` gains a `Narration` value (data model change, zero behavior change for existing senders). `ChatBubble` becomes a small explicit state machine (`Idle` → `Typing` → `Completed`) driven by a single public entry point `Play(sender, text)`, using a DOTween integer tween to reveal characters while keeping the string length constant via space-padding (so `ContentSizeFitter` never resizes mid-type). `Skip()` force-completes an in-progress typing tween.

**Tech Stack:** Unity (C#), TextMeshPro, DOTween (already vendored at `Assets/Plugins/Demigiant`), NUnit EditMode tests, Newtonsoft.Json (existing `GeminiProvider` tests).

## Global Constraints

- `ChatSender` currently has `Player`, `Npc` only (`Assets/02_Script/Runtime/Core/ChatMessage.cs:3-7`) — add `Narration` without renumbering existing values.
- `ChatBubble.cs` stays at `Assets/ChatBubble.cs` (Assembly-CSharp / no asmdef) — it is a MonoBehaviour needing `UnityEngine.UI`, `TMPro`, and DOTween, none of which `TextingRPG.Runtime.asmdef` currently references. Do not move the file or add engine references to that asmdef; that's an unrelated, riskier change.
- Because `ChatBubble.cs` lives outside any asmdef, `TextingRPG.EditModeTests.asmdef` (which references only `TextingRPG.Runtime`) cannot compile against it — this matches the design spec's own testing section ("자동화된 테스트는 두지 않는다"). No EditMode test file for `ChatBubble` itself; verify by confirming Unity compiles with no new console errors.
- `charsPerSecond` default is `40f` (serialized field, tunable per-prefab in the Inspector — do not hardcode elsewhere).
- Keep the existing three serialized fields (`verticalLayoutGroup`, `rectTransform_Bubble`, `textMeshProUGUI_text`) as-is; `rectTransform_Bubble` stays unused by this spec (reserved for future work per the design doc's own sample).
- DOTween's integer tween overload (`DOTween.To(() => int, v => ..., int, float)`) is built in — no DOTweenPro plugin needed for this feature.

---

### Task 1: Add `ChatSender.Narration` and lock in its LLM-role mapping

**Files:**
- Modify: `Assets/02_Script/Runtime/Core/ChatMessage.cs:3-7`
- Test: `Assets/02_Script/Tests/EditMode/GeminiProviderTests.cs`

**Interfaces:**
- Produces: `TextingRPG.Core.ChatSender.Narration` (new enum member), consumed by Task 2's `ChatBubble.AlignmentFor`.

- [ ] **Step 1: Write the failing test**

Add to `Assets/02_Script/Tests/EditMode/GeminiProviderTests.cs`, inside the `GeminiProviderTests` class:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode test suite (Unity Test Runner, or via `mcp__UnityMCP__run_tests` if available) filtered to `GeminiProviderTests`.
Expected: compile error — `ChatSender` does not contain a definition for `Narration`.

- [ ] **Step 3: Add the enum value**

In `Assets/02_Script/Runtime/Core/ChatMessage.cs`, change:

```csharp
    public enum ChatSender
    {
        Player,
        Npc
    }
```

to:

```csharp
    public enum ChatSender
    {
        Player,
        Npc,
        Narration
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run the same EditMode test filter as Step 2.
Expected: `BuildRequestBody_MapsNarrationSenderToModelRole` PASSES, and the two pre-existing `GeminiProviderTests` still PASS (no regression from the new enum member).

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/Core/ChatMessage.cs Assets/02_Script/Tests/EditMode/GeminiProviderTests.cs
git commit -m "feat: add Narration chat sender"
```

---

### Task 2: Implement ChatBubble state machine and typewriter reveal

**Files:**
- Modify: `Assets/ChatBubble.cs` (currently field-only skeleton, untracked)

**Interfaces:**
- Consumes: `TextingRPG.Core.ChatSender` (`Player`/`Npc`/`Narration`, from Task 1).
- Produces: `TextingRPG.UI.ChatBubbleState { Idle, Typing, Completed }`; `ChatBubble.State` (public read-only property); `ChatBubble.OnPlayComplete` (`event Action`); `ChatBubble.Play(ChatSender sender, string text)`; `ChatBubble.Skip()`. These are the exact names/signatures a future list controller (out of scope here) will call.

- [ ] **Step 1: Replace the contents of `Assets/ChatBubble.cs`**

```csharp
using System;
using DG.Tweening;
using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public enum ChatBubbleState
    {
        Idle,
        Typing,
        Completed
    }

    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] VerticalLayoutGroup verticalLayoutGroup;
        [SerializeField] RectTransform rectTransform_Bubble;
        [SerializeField] TextMeshProUGUI textMeshProUGUI_text;
        [SerializeField] float charsPerSecond = 40f;

        public ChatBubbleState State { get; private set; } = ChatBubbleState.Idle;
        public event Action OnPlayComplete;

        private Tween _typingTween;
        private string _fullText;

        public void Play(ChatSender sender, string text)
        {
            verticalLayoutGroup.childAlignment = AlignmentFor(sender);
            _typingTween?.Kill();

            if (sender == ChatSender.Player)
            {
                textMeshProUGUI_text.text = text;
                SetState(ChatBubbleState.Completed);
                return;
            }

            StartTyping(text);
        }

        public void Skip()
        {
            if (State != ChatBubbleState.Typing) return;
            _typingTween?.Kill();
            textMeshProUGUI_text.text = _fullText;
            SetState(ChatBubbleState.Completed);
        }

        private void StartTyping(string text)
        {
            _fullText = text;
            SetState(ChatBubbleState.Typing);
            textMeshProUGUI_text.text = new string(' ', text.Length);

            int revealed = 0;
            float duration = Mathf.Max(0.01f, text.Length / charsPerSecond);
            _typingTween = DOTween.To(() => revealed, v => revealed = v, text.Length, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => textMeshProUGUI_text.text = text.Substring(0, revealed) + new string(' ', text.Length - revealed))
                .OnComplete(() => SetState(ChatBubbleState.Completed));
        }

        private void SetState(ChatBubbleState state)
        {
            State = state;
            if (state == ChatBubbleState.Completed) OnPlayComplete?.Invoke();
        }

        private static TextAnchor AlignmentFor(ChatSender sender) => sender switch
        {
            ChatSender.Player => TextAnchor.UpperRight,
            ChatSender.Narration => TextAnchor.UpperCenter,
            _ => TextAnchor.UpperLeft
        };
    }
}
```

- [ ] **Step 2: Refresh Unity and check the console**

Trigger a recompile (`mcp__UnityMCP__refresh_unity` or focus the Unity Editor) then read the console (`mcp__UnityMCP__read_console`).
Expected: no new compile errors. A pre-existing "unused field `rectTransform_Bubble`" warning is expected and fine (see Global Constraints — it's reserved for future work, not this spec's concern).

- [ ] **Step 3: Manual verification (documented, not automated)**

This cannot be automated per the design spec's own testing section (DOTween + MonoBehaviour lifecycle isn't EditMode-testable), and no `ChatBubble` prefab/scene exists yet to drive a Play-mode smoke test — building one is scoped to the future list-controller work, not this task. Record for whoever wires up the first prefab:

- Call `Play(ChatSender.Player, "안녕")` → text shows immediately, `State == Completed`, alignment `UpperRight`.
- Call `Play(ChatSender.Npc, "어서오세요")` → text reveals left-to-right over ~`text.Length / 40` seconds, `State == Typing` during, `UpperLeft` alignment, `State == Completed` at the end, `OnPlayComplete` fires once.
- Call `Play(ChatSender.Narration, "밤이 깊어간다")` → same as Npc but `UpperCenter` alignment.
- Call `Skip()` mid-typing → text jumps to full immediately, `State == Completed`, `OnPlayComplete` fires.
- Call `Skip()` while `Idle` or already `Completed` → no-op (no exception, no duplicate `OnPlayComplete`).

- [ ] **Step 4: Commit**

```bash
git add Assets/ChatBubble.cs
git commit -m "feat: implement ChatBubble typing animation state machine"
```
