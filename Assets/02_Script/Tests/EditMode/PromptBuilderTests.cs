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

        [Test]
        public void BuildTurnPrompt_HasPacingAndEndingFloorRules()
        {
            var outline = new DataManager.StoryOutline
            {
                Title = "T", WorldSetting = "W", CentralConflict = "왕과 반란군의 대립",
                KeyEvents = new List<string> { "반란 시작", "왕 암살 시도" }
            };

            var prompt = PromptBuilder.BuildTurnPrompt(outline, new List<string>(), "카린");

            StringAssert.Contains("매 턴 이야기는 반드시 한 발 나아간다", prompt);
            StringAssert.Contains("충분히 전개된 뒤에만 끝난다", prompt);
            StringAssert.Contains("endingTone", prompt);
            StringAssert.Contains("[핵심 갈등]", prompt);
            StringAssert.Contains("왕과 반란군의 대립", prompt);
        }

        [Test]
        public void BuildStoryOutlinePrompt_AsksForOrderedEscalationAndConflict()
        {
            var prompt = PromptBuilder.BuildStoryOutlinePrompt("어두운 숲", "카린");

            StringAssert.Contains("centralConflict", prompt);
            StringAssert.Contains("순서대로", prompt);
            StringAssert.Contains("전환점", prompt);
        }
    }
}
