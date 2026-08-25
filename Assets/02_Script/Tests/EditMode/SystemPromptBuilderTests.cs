using NUnit.Framework;
using TextingRPG.NPC;
using TextingRPG.Story;
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

        private static StoryNode MakeNode()
        {
            var node = new StoryNode { NodeId = "intro", SceneDescription = "플레이어가 막 상점에 들어왔다." };
            node.Transitions.Add(new StoryTransition { Tag = "friendly", NextNodeId = "trust" });
            node.Transitions.Add(new StoryTransition { Tag = "hostile", NextNodeId = "conflict" });
            return node;
        }

        [Test]
        public void Build_IncludesWorldDescriptionPersonaAndSceneDescription()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "이곳은 중세 판타지 마을이다.", MakeNode());

            StringAssert.Contains("이곳은 중세 판타지 마을이다.", prompt);
            StringAssert.Contains("무뚝뚝하지만 정 많은 상인이다.", prompt);
            StringAssert.Contains("상인 미라", prompt);
            StringAssert.Contains("플레이어가 막 상점에 들어왔다.", prompt);
        }

        [Test]
        public void Build_IncludesAllowedTagsFromCurrentNode()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode());

            StringAssert.Contains("friendly", prompt);
            StringAssert.Contains("hostile", prompt);
        }

        [Test]
        public void Build_IncludesCharacterGuardrailInstruction()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode());

            StringAssert.Contains("짧은 메신저 메시지", prompt);
        }

        [Test]
        public void Build_IncludesNarrationAndNpcLineInstruction()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode());

            StringAssert.Contains("narration", prompt);
            StringAssert.Contains("npcLine", prompt);
        }

        [Test]
        public void Build_WithEndingHint_AppendsHintToPrompt()
        {
            var prompt = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode(), "이번이 마지막 턴이다.");

            StringAssert.Contains("이번이 마지막 턴이다.", prompt);
        }

        [Test]
        public void Build_WithoutEndingHint_SameAsEmptyStringHint()
        {
            var promptWithoutArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode());
            var promptWithEmptyArg = SystemPromptBuilder.Build(MakeNpc(), "세계관", MakeNode(), "");

            Assert.AreEqual(promptWithoutArg, promptWithEmptyArg);
        }
    }
}
