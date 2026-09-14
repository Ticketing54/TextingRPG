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
            StringAssert.Contains("offTopic", body);
            StringAssert.Contains("endingTone", body);
        }

        [Test]
        public void ParseTurnResponse_ReadsEndingTone()
        {
            var good = "{\"narration\":\"끝\",\"newFacts\":[],\"isEnding\":true,\"endingTone\":\"good\",\"offTopic\":false,\"choices\":[]}";
            var missing = "{\"narration\":\"n\",\"newFacts\":[],\"isEnding\":false,\"choices\":[]}";

            Assert.AreEqual("good", GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(good)).EndingTone);
            Assert.AreEqual("", GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(missing)).EndingTone);
        }

        [Test]
        public void StoryOutline_SchemaAndParse_IncludeCentralConflict()
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

            StringAssert.Contains("centralConflict", provider.BuildStoryOutlineRequestBody(context));

            var payload =
                "{\"title\":\"T\",\"worldSetting\":\"W\",\"centralConflict\":\"왕과 반란군의 대립\"," +
                "\"keyCharacters\":[],\"keyEvents\":[],\"finalGoal\":\"G\",\"openingNarration\":\"O\"}";
            var (outline, _) = GeminiProvider.ParseStoryOutline(WrapAsGeminiResponse(payload));

            Assert.AreEqual("왕과 반란군의 대립", outline.CentralConflict);
        }

        [Test]
        public void ParseTurnResponse_ReadsOffTopicFlag()
        {
            var onTopic = "{\"narration\":\"n\",\"newFacts\":[],\"isEnding\":false,\"offTopic\":false,\"choices\":[]}";
            var offTopic = "{\"narration\":\"딴 얘기 말고 이야기로 돌아오자.\",\"newFacts\":[],\"isEnding\":false,\"offTopic\":true,\"choices\":[]}";
            var missing = "{\"narration\":\"n\",\"newFacts\":[],\"isEnding\":false,\"choices\":[]}";

            Assert.IsFalse(GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(onTopic)).IsOffTopic);
            Assert.IsTrue(GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(offTopic)).IsOffTopic);
            Assert.IsFalse(GeminiProvider.ParseTurnResponse(WrapAsGeminiResponse(missing)).IsOffTopic);
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
