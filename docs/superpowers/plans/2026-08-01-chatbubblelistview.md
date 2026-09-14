# ChatBubbleListView Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `ChatBubbleListView`, a MonoBehaviour that turns `ChatController.OnMessageAdded` events into `ChatBubble` instances played one at a time in a queue, scrolling the list to the bottom as each one appears, per `docs/superpowers/specs/2026-08-01-chatbubblelistview-design.md`.

**Architecture:** `ChatBubbleListView` exposes a single `Bind(ChatController controller)` entry point (constructor injection isn't possible since `ChatController` is a plain C# class, not serializable). It queues incoming `ChatMessage`s and only instantiates/plays the next `ChatBubble` when no bubble is currently playing, advancing the queue via the just-finished bubble's `OnPlayComplete` event.

**Tech Stack:** Unity (C#), `UnityEngine.UI.ScrollRect`, existing `ChatBubble`/`ChatController`/`ChatMessage` types.

## Global Constraints

- New file: `Assets/02_Script/Runtime/UI/ChatBubbleListView.cs`, namespace `TextingRPG.UI` — same folder/namespace as `ChatController.cs` and `ChatBubble.cs`, so no `using` needed for those two types; still needs `using TextingRPG.Core;` for `ChatMessage`.
- No new asmdef references needed: `TextingRPG.Runtime.asmdef` already references `UnityEngine.UI` (added when `ChatBubble` moved in) and `ScrollRect` lives in that assembly.
- No automated tests for this file — same reasoning as `ChatBubble` (MonoBehaviour + `Instantiate` + DOTween-driven `ChatBubble` isn't EditMode-testable). Verify via clean Unity compile only; record manual Play-mode steps for whoever wires up the scene.
- Do not build the game bootstrap that constructs `ChatController` (`PlayerState`/`ILLMProvider`/`NPCDefinition`/`StoryGraph`) — out of scope per spec. `Bind` takes an already-constructed `ChatController`.
- Always scroll to the bottom on every new bubble (no "only if already at bottom" logic) — explicitly out of scope per spec.
- No object pooling — `Instantiate` a fresh `ChatBubble` per message.

---

### Task 1: Implement ChatBubbleListView queue playback

**Files:**
- Create: `Assets/02_Script/Runtime/UI/ChatBubbleListView.cs`

**Interfaces:**
- Consumes: `TextingRPG.UI.ChatController.OnMessageAdded` (`event Action<ChatMessage>`, from `ChatController.cs:19`); `TextingRPG.UI.ChatBubble.Play(ChatSender, string)`, `ChatBubble.OnPlayComplete` (`event Action`), both from `ChatBubble.cs`.
- Produces: `ChatBubbleListView.Bind(ChatController controller)` — the only public entry point.

- [ ] **Step 1: Write `ChatBubbleListView.cs`**

```csharp
using System.Collections.Generic;
using TextingRPG.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class ChatBubbleListView : MonoBehaviour
    {
        [SerializeField] ChatBubble chatBubblePrefab;
        [SerializeField] RectTransform content;
        [SerializeField] ScrollRect scrollRect;

        private readonly Queue<ChatMessage> _pending = new Queue<ChatMessage>();
        private ChatBubble _current;
        private ChatController _controller;

        public void Bind(ChatController controller)
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
            _controller = controller;
            _controller.OnMessageAdded += Enqueue;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
        }

        private void Enqueue(ChatMessage message)
        {
            _pending.Enqueue(message);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_current != null) return;
            if (_pending.Count == 0) return;

            var message = _pending.Dequeue();
            _current = Instantiate(chatBubblePrefab, content);
            _current.OnPlayComplete += HandleCurrentComplete;
            _current.Play(message.Sender, message.Text);

            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void HandleCurrentComplete()
        {
            _current.OnPlayComplete -= HandleCurrentComplete;
            _current = null;
            TryPlayNext();
        }
    }
}
```

- [ ] **Step 2: Refresh Unity and check the console**

Trigger a recompile (`mcp__UnityMCP__refresh_unity` with `compile: request`, `mode: force`) then read the console (`mcp__UnityMCP__read_console` filtered to `types: ["error"]`).
Expected: no compile errors.

- [ ] **Step 3: Run the EditMode suite as a regression check**

Run `mcp__UnityMCP__run_tests` with `mode: EditMode`, poll via `mcp__UnityMCP__get_test_job`.
Expected: all existing tests still pass (this file doesn't touch anything they cover, but confirms the domain reload succeeded cleanly).

- [ ] **Step 4: Manual verification (documented, not automated)**

Cannot be automated per the design spec's own testing section. Record for whoever wires this into the scene (attaching to a GameObject, assigning `chatBubblePrefab`/`content`/`scrollRect`, and calling `Bind` with a real or stub `ChatController`):

- Fire `OnMessageAdded` for a `Player` message, then immediately for an `Npc` message (simulating a fast round trip) → the `Player` bubble appears instantly (synchronous `Completed`), and the `Npc` bubble starts typing right after — not before, not overlapping.
- Fire three `Npc`/`Narration` messages back-to-back → they type out one at a time, in the order they were fired; only one bubble is ever mid-typing.
- After each bubble appears, the `ScrollRect` is pinned to the bottom (`verticalNormalizedPosition == 0`).
- Call `Bind` twice with two different `ChatController` instances → messages from the first controller (fired after the second `Bind` call) no longer trigger new bubbles.

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/UI/ChatBubbleListView.cs
git commit -m "feat: add ChatBubbleListView for queued bubble playback"
```
