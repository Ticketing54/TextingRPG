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
                            { ""text"": ""{\""narration\"":\""게르트가 손님을 맞이한다.\"",\""npcLine\"":\""어서오세요, 손님!\"",\""tags\"":[\""friendly\""],\""effects\"":[{\""type\"":\""relationship\"",\""target\"":\""npc_a\"",\""delta\"":1.0}]}"" }
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
                            { ""text"": ""{\""narration\"":\""문이 닫힌다.\"",\""npcLine\"":\""\"",\""tags\"":[],\""effects\"":[]}"" }
                        ]
                    }
                }
            ]
        }";

        [Test]
        public void ParseResponse_ExtractsNarrationNpcLineTagsAndEffectsFromNestedJsonText()
        {
            var response = GeminiProvider.ParseResponse(SampleGeminiResponse);

            Assert.AreEqual("게르트가 손님을 맞이한다.", response.Narration);
            Assert.AreEqual("어서오세요, 손님!", response.NpcLine);
            CollectionAssert.AreEqual(new[] { "friendly" }, response.Tags);
            Assert.AreEqual(1, response.Effects.Count);
            Assert.AreEqual("relationship", response.Effects[0].Type);
            Assert.AreEqual("npc_a", response.Effects[0].Target);
            Assert.AreEqual(1.0f, response.Effects[0].Delta);
        }

        [Test]
        public void ParseResponse_EmptyNpcLine_ReturnsEmptyString()
        {
            var response = GeminiProvider.ParseResponse(SampleGeminiResponseWithoutNpcLine);

            Assert.AreEqual("문이 닫힌다.", response.Narration);
            Assert.AreEqual("", response.NpcLine);
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

            var requiredFields = ((JArray)body["generationConfig"]["responseSchema"]["required"])
                .Select(t => (string)t)
                .ToList();
            CollectionAssert.Contains(requiredFields, "tags");
            CollectionAssert.Contains(requiredFields, "narration");
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
