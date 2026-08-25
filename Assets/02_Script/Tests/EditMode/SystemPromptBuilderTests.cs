using NUnit.Framework;
using TextingRPG.NPC;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class SystemPromptBuilderTests
    {
        private static NPCDefinition MakeNpc()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";
            return npc;
        }

        [Test]
        public void Build_IncludesWorldDescriptionPersonaAndSummary()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "이곳은 중세 판타지 마을이다.", "플레이어가 막 상점에 들어왔다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("무뚝뚝하지만 정 많은 상인이다.", prompt);
            StringAssert.Contains("상인 미라", prompt);
            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", prompt);
        }

        [Test]
        public void Build_WithEmptySummary_UsesNothingHappenedYetPlaceholder()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", "");

            StringAssert.Contains("아직 아무 일도 일어나지 않았다.", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약");

            StringAssert.Contains("짧은 메신저 메시지", prompt);
        }

        [Test]
        public void Build_IncludesNarrationAndNpcLineInstruction()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약");

            StringAssert.Contains("narration", prompt);
            StringAssert.Contains("npcLine", prompt);
        }

        [Test]
        public void Build_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약");

            StringAssert.Contains("5문장 이내", prompt);
        }

        [Test]
        public void Build_WithEndingHint_AppendsHintToPrompt()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약", "이번이 마지막 턴이다.");

            StringAssert.Contains("이번이 마지막 턴이다.", prompt);
        }

        [Test]
        public void Build_WithoutEndingHint_SameAsEmptyStringHint()
        {
            var promptWithoutArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약");
            var promptWithEmptyArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", "요약", "");

            Assert.AreEqual(promptWithoutArg, promptWithEmptyArg);
        }

        [Test]
        public void BuildOpening_IncludesWorldDescriptionAndPersona()
        {
            var prompt = SystemPromptBuilder.BuildOpening(MakeNpc(), "이곳은 중세 판타지 마을이다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("무뚝뚝하지만 정 많은 상인이다.", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSituationAndGoalAwareness()
        {
            var prompt = SystemPromptBuilder.BuildOpening(MakeNpc(), "세계관");

            StringAssert.Contains("무엇을 하면 좋을지", prompt);
        }

        [Test]
        public void BuildOpening_InstructsEmptyEffects()
        {
            var prompt = SystemPromptBuilder.BuildOpening(MakeNpc(), "세계관");

            StringAssert.Contains("effects는", prompt);
        }

        [Test]
        public void BuildOpening_InstructsSummaryLengthCap()
        {
            var prompt = SystemPromptBuilder.BuildOpening(MakeNpc(), "세계관");

            StringAssert.Contains("5문장 이내", prompt);
        }
    }
}
