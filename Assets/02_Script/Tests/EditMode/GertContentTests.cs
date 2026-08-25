using NUnit.Framework;
using TextingRPG.NPC;
using UnityEditor;

namespace TextingRPG.Tests
{
    public class GertContentTests
    {
        private const string NpcAssetPath = "Assets/05_Data/NPC/Gert.asset";

        [Test]
        public void GertNpcAsset_HasExpectedFields()
        {
            var npc = AssetDatabase.LoadAssetAtPath<NPCDefinition>(NpcAssetPath);

            Assert.IsNotNull(npc, $"{NpcAssetPath} 에셋을 찾을 수 없습니다.");
            Assert.AreEqual("innkeeper_gert", npc.NpcId);
            Assert.AreEqual("게르트", npc.DisplayName);
            StringAssert.Contains("여관 주인", npc.PersonaDescription);
        }
    }
}
