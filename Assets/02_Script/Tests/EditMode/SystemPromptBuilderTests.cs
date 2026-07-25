using NUnit.Framework;
using TextingRPG.NPC;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class SystemPromptBuilderTests
    {
        [Test]
        public void Build_IncludesWorldDescriptionAndPersona()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";

            var prompt = SystemPromptBuilder.Build(npc, "이곳은 중세 판타지 마을이다.");

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("무뚝뚝하지만 정 많은 상인이다.", prompt);
            StringAssert.Contains("상인 미라", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "npc_a";
            npc.DisplayName = "상인 미라";
            npc.PersonaDescription = "무뚝뚝하지만 정 많은 상인이다.";

            var prompt = SystemPromptBuilder.Build(npc, "세계관");

            StringAssert.Contains("상인 미라", prompt);
            Assert.IsTrue(prompt.Length > "세계관".Length + "무뚝뚝하지만 정 많은 상인이다.".Length,
                "가드레일 문구가 포함되어 있어야 한다.");
        }
    }
}
