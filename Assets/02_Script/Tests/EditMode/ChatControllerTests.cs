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
