using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Systems;

namespace TextingRPG.Tests
{
    public class PromptBuilderTests
    {
        [Test]
        public void BuildStoryOutlinePrompt_IncludesPlayerName()
        {
            var prompt = PromptBuilder.BuildStoryOutlinePrompt("어두운 숲", "카린");

            StringAssert.Contains("[플레이어 이름]", prompt);
            StringAssert.Contains("카린", prompt);
        }

        [Test]
        public void BuildTurnPrompt_IncludesPlayerNameAndExtraHint()
        {
            var outline = new DataManager.StoryOutline { Title = "T", WorldSetting = "W" };

            var prompt = PromptBuilder.BuildTurnPrompt(outline, new List<string>(), "카린", "플레이어 선택: 문을 연다");

            StringAssert.Contains("[플레이어 이름]", prompt);
            StringAssert.Contains("카린", prompt);
            StringAssert.Contains("플레이어 선택: 문을 연다", prompt);
        }

        [Test]
        public void BuildTurnPrompt_HasOffTopicRule_AndDoesNotForceSafeChoice()
        {
            var outline = new DataManager.StoryOutline { Title = "T", WorldSetting = "W" };

            var prompt = PromptBuilder.BuildTurnPrompt(outline, new List<string>(), "카린");

            StringAssert.Contains("offTopic", prompt);
            StringAssert.Contains("습관적으로 안전한 선택지를 넣지 않는다", prompt);
        }
    }
}
