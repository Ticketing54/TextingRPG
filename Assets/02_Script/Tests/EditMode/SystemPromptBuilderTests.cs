using NUnit.Framework;
using TextingRPG.NPC;

namespace TextingRPG.Tests
{
    public class SystemPromptBuilderTests
    {
        [Test]
        public void Build_IncludesWorldDescriptionAndSummary()
        {
            var prompt = SystemPromptBuilder.Build("이곳은 중세 판타지 마을이다.", "플레이어가 막 상점에 들어왔다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", prompt);
        }

        [Test]
        public void Build_WithEmptySummary_UsesNothingHappenedYetPlaceholder()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "");

            StringAssert.Contains("아직 아무 일도 일어나지 않았다.", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("짧은 메신저 메시지", prompt);
        }

        [Test]
        public void Build_IncludesNarrationAndNpcLineInstruction()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("narration", prompt);
            StringAssert.Contains("npcLine", prompt);
        }

        [Test]
        public void Build_InstructsNpcLineDefaultsToEmpty()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("npcLine은 기본적으로 비워", prompt);
        }

        [Test]
        public void Build_InstructsRelationshipTargetIsCharacterName()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("target에는 호감도가 바뀌는 대상 캐릭터의 이름", prompt);
        }

        [Test]
        public void Build_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("5문장 이내", prompt);
        }

        [Test]
        public void Build_InstructsGeneralIsEndingJudgement()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약");

            StringAssert.Contains("isEnding을", prompt);
            StringAssert.Contains("사망, 만족스러운 결말", prompt);
        }

        [Test]
        public void Build_WithExtraHint_AppendsHintToPrompt()
        {
            var prompt = SystemPromptBuilder.Build("세계관", "요약", "이번이 마지막 턴이다.");

            StringAssert.Contains("이번이 마지막 턴이다.", prompt);
        }

        [Test]
        public void Build_WithoutExtraHint_SameAsEmptyStringHint()
        {
            var promptWithoutArg = SystemPromptBuilder.Build("세계관", "요약");
            var promptWithEmptyArg = SystemPromptBuilder.Build("세계관", "요약", "");

            Assert.AreEqual(promptWithoutArg, promptWithEmptyArg);
        }

        [Test]
        public void BuildOpening_IncludesWorldDescription()
        {
            var prompt = SystemPromptBuilder.BuildOpening("이곳은 중세 판타지 마을이다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSituationAndGoalAwareness()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("무엇을 하면 좋을지", prompt);
        }

        [Test]
        public void BuildOpening_InstructsNpcLineDefaultsToEmpty()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("npcLine은 기본적으로 비워", prompt);
        }

        [Test]
        public void BuildOpening_InstructsEmptyEffectsAndIsEnding()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("effects는", prompt);
            StringAssert.Contains("isEnding도 항상 false", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.BuildOpening("세계관");

            StringAssert.Contains("5문장 이내", prompt);
        }
    }
}
