using NUnit.Framework;
using TextingRPG.LLM;

namespace TextingRPG.Tests
{
    public class GeminiProviderChoicesTests
    {
        private static string WrapAsGeminiResponse(string payloadJson)
        {
            var escaped = payloadJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"" + escaped + "\"}]}}]}";
        }

        [Test]
        public void BuildTurnRequestBody_IncludesChoicesInSchema()
        {
            var provider = new GeminiProvider("key", "model");
            var context = new ConversationContext
            {
                SystemPrompt = "sys",
                History = new System.Collections.Generic.List<TextingRPG.Core.ChatMessage>
                {
                    new TextingRPG.Core.ChatMessage(TextingRPG.Core.ChatSender.Player, "hi", "t")
                }
            };

            var body = provider.BuildTurnRequestBody(context);

            StringAssert.Contains("choices", body);
            StringAssert.Contains("안전", body);
            StringAssert.Contains("위험", body);
            StringAssert.Contains("무모", body);
        }

        [Test]
        public void ParseTurnResponse_ReadsChoicesWithRisk()
        {
            var payload =
                "{\"narration\":\"n\",\"npcLine\":\"\",\"newFacts\":[],\"isEnding\":false," +
                "\"choices\":[{\"text\":\"기다린다\",\"risk\":\"안전\"},{\"text\":\"자물쇠를 딴다\",\"risk\":\"위험\"}," +
                "{\"text\":\"문을 부순다\",\"risk\":\"무모\"}]}";

            var response = GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(payload));

            Assert.AreEqual(3, response.Choices.Count);
            Assert.AreEqual("기다린다", response.Choices[0].Text);
            Assert.AreEqual(ChoiceRisk.Safe, response.Choices[0].Risk);
            Assert.AreEqual(ChoiceRisk.Risky, response.Choices[1].Risk);
            Assert.AreEqual(ChoiceRisk.Reckless, response.Choices[2].Risk);
        }

        [Test]
        public void ParseTurnResponse_StripsLeadingRiskTagFromChoiceText()
        {
            var payload =
                "{\"narration\":\"n\",\"newFacts\":[],\"isEnding\":false,\"choices\":[" +
                "{\"text\":\"[안전] 기다린다\",\"risk\":\"안전\"}," +
                "{\"text\":\"[위험]자물쇠를 딴다\",\"risk\":\"위험\"}," +
                "{\"text\":\"문을 부순다\",\"risk\":\"무모\"}]}";

            var response = GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(payload));

            Assert.AreEqual("기다린다", response.Choices[0].Text);
            Assert.AreEqual("자물쇠를 딴다", response.Choices[1].Text);
            Assert.AreEqual("문을 부순다", response.Choices[2].Text);
        }

        [Test]
        public void ParseTurnResponse_NoChoices_LeavesEmptyList()
        {
            var payload = "{\"narration\":\"n\",\"newFacts\":[],\"isEnding\":true}";

            var response = GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(payload));

            Assert.IsNotNull(response.Choices);
            Assert.AreEqual(0, response.Choices.Count);
        }
    }
}
