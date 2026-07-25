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
