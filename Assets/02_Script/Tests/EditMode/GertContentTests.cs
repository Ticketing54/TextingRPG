using System.Linq;
using NUnit.Framework;
using TextingRPG.NPC;
using TextingRPG.Story;
using UnityEditor;

namespace TextingRPG.Tests
{
    public class GertContentTests
    {
        private const string NpcAssetPath = "Assets/05_Data/NPC/Gert.asset";
        private const string StoryAssetPath = "Assets/05_Data/Story/GertStoryGraph.asset";

        [Test]
        public void GertNpcAsset_HasExpectedFields()
        {
            var npc = AssetDatabase.LoadAssetAtPath<NPCDefinition>(NpcAssetPath);

            Assert.IsNotNull(npc, $"{NpcAssetPath} 에셋을 찾을 수 없습니다.");
            Assert.AreEqual("innkeeper_gert", npc.NpcId);
            Assert.AreEqual("게르트", npc.DisplayName);
            StringAssert.Contains("여관 주인", npc.PersonaDescription);
        }

        [Test]
        public void GertStoryGraph_HasExpectedNodesAndTransitions()
        {
            var graph = AssetDatabase.LoadAssetAtPath<StoryGraph>(StoryAssetPath);

            Assert.IsNotNull(graph, $"{StoryAssetPath} 에셋을 찾을 수 없습니다.");
            Assert.AreEqual("innkeeper_gert", graph.NpcId);
            Assert.AreEqual("start", graph.StartNodeId);
            Assert.AreEqual(6, graph.Nodes.Count);

            var nodeIds = graph.Nodes.Select(n => n.NodeId).ToList();
            CollectionAssert.AreEquivalent(
                new[] { "start", "chat", "wary", "room", "rumor", "kicked_out" }, nodeIds);

            var start = graph.GetNode("start");
            CollectionAssert.AreEquivalent(new[] { "friendly", "hostile" }, start.AllowedTags);
            Assert.AreEqual("chat", start.Transitions.Single(t => t.Tag == "friendly").NextNodeId);
            Assert.AreEqual("wary", start.Transitions.Single(t => t.Tag == "hostile").NextNodeId);

            var chat = graph.GetNode("chat");
            Assert.AreEqual("room", chat.Transitions.Single(t => t.Tag == "ask_room").NextNodeId);
            Assert.AreEqual("rumor", chat.Transitions.Single(t => t.Tag == "ask_rumor").NextNodeId);

            var wary = graph.GetNode("wary");
            Assert.AreEqual("chat", wary.Transitions.Single(t => t.Tag == "apologize").NextNodeId);
            Assert.AreEqual("kicked_out", wary.Transitions.Single(t => t.Tag == "threaten").NextNodeId);

            var room = graph.GetNode("room");
            Assert.AreEqual("rumor", room.Transitions.Single(t => t.Tag == "ask_rumor").NextNodeId);
            Assert.AreEqual("chat", room.Transitions.Single(t => t.Tag == "friendly").NextNodeId);

            var rumor = graph.GetNode("rumor");
            Assert.AreEqual("room", rumor.Transitions.Single(t => t.Tag == "ask_room").NextNodeId);
            Assert.AreEqual("chat", rumor.Transitions.Single(t => t.Tag == "friendly").NextNodeId);

            Assert.AreEqual(0, graph.GetNode("kicked_out").Transitions.Count);
        }
    }
}
