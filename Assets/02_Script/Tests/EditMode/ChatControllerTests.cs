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
