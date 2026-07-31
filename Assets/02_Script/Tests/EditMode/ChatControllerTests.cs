using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;
using TextingRPG.UI;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class ChatControllerTests
    {
        private static NPCDefinition MakeNpc()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";
            return npc;
        }

        private static StoryGraph MakeGraph()
        {
            var intro = new StoryNode { NodeId = "intro", SceneDescription = "플레이어가 막 상점에 들어왔다." };
            intro.Transitions.Add(new StoryTransition { Tag = "friendly", NextNodeId = "trust" });
            var trust = new StoryNode { NodeId = "trust", SceneDescription = "손님이 마음에 들기 시작했다." };

            var graph = ScriptableObject.CreateInstance<StoryGraph>();
            graph.NpcId = "npc_a";
            graph.StartNodeId = "intro";
            graph.Nodes = new List<StoryNode> { intro, trust };
            return graph;
        }

        [Test]
        public void SendPlayerMessage_AppendsPlayerMessageToHistoryImmediately()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            controller.SendPlayerMessage("안녕하세요");

            var history = state.GetHistory("npc_a");
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
            Assert.AreEqual("안녕하세요", history[0].Text);
        }

        [Test]
        public void SendPlayerMessage_InitializesStoryNodeToGraphStartNode()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual("intro", state.GetStoryNode("npc_a"));
        }

        [Test]
        public void SendPlayerMessage_SystemPromptIncludesCurrentNodeSceneDescription()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            controller.SendPlayerMessage("안녕하세요");

            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendPlayerMessage_OnSuccess_AppendsReplyAppliesEffectsAndAdvancesStoryNode()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse
                {
                    Reply = "반가워요!",
                    Tags = new List<string> { "friendly" },
                    Effects = new List<LLMEffect> { new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 2f } }
                }
            };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            string changedNode = null;
            controller.OnStoryNodeChanged += n => changedNode = n;

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual(2, state.GetHistory("npc_a").Count);
            Assert.AreEqual(2, state.GetRelationship("npc_a"));
            Assert.AreEqual("trust", state.GetStoryNode("npc_a"));
            Assert.AreEqual("trust", changedNode);
        }

        [Test]
        public void SendPlayerMessage_NoMatchingTag_StaysOnSameNodeAndDoesNotFireStoryNodeChanged()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Reply = "음...", Tags = new List<string> { "neutral" } }
            };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            var fired = false;
            controller.OnStoryNodeChanged += _ => fired = true;

            controller.SendPlayerMessage("아무말");

            Assert.AreEqual("intro", state.GetStoryNode("npc_a"));
            Assert.IsFalse(fired);
        }

        [Test]
        public void SendPlayerMessage_OnlySendsMessagesWithinHistoryWindow()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "ok" } };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            for (int i = 0; i < 10; i++)
            {
                controller.SendPlayerMessage($"메시지 {i}");
            }

            Assert.AreEqual(8, provider.LastContext.History.Count);
        }

        [Test]
        public void SendPlayerMessage_OnProviderError_FiresOnErrorAndDoesNotAdvanceNode()
        {
            var state = new PlayerState();
            var provider = new MockLLMProvider { NextError = "network down" };
            var controller = new ChatController(state, provider, MakeNpc(), "세계관", MakeGraph());

            string capturedError = null;
            controller.OnError += e => capturedError = e;

            controller.SendPlayerMessage("안녕하세요");

            Assert.AreEqual("network down", capturedError);
            Assert.AreEqual(1, state.GetHistory("npc_a").Count);
            Assert.AreEqual("intro", state.GetStoryNode("npc_a"));
        }
    }
}
