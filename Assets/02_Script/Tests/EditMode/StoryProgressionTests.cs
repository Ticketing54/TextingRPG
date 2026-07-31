using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace TextingRPG.Tests
{
    public class StoryProgressionTests
    {
        private static StoryNode MakeNode(string id, params (string tag, string next)[] transitions)
        {
            var node = new StoryNode { NodeId = id, SceneDescription = "desc" };
            foreach (var (tag, next) in transitions)
            {
                node.Transitions.Add(new StoryTransition { Tag = tag, NextNodeId = next });
            }
            return node;
        }

        [Test]
        public void Resolve_MatchingTag_ReturnsNextNodeId()
        {
            var node = MakeNode("intro", ("friendly", "trust"), ("hostile", "conflict"));

            var next = StoryProgression.Resolve(node, new[] { "friendly" }, out var applied);

            Assert.AreEqual("trust", next);
            Assert.AreEqual("friendly", applied);
        }

        [Test]
        public void Resolve_NoMatchingTag_StaysOnSameNode()
        {
            var node = MakeNode("intro", ("friendly", "trust"));

            var next = StoryProgression.Resolve(node, new[] { "unknown_tag" }, out var applied);

            Assert.AreEqual("intro", next);
            Assert.IsNull(applied);
        }

        [Test]
        public void Resolve_UnknownTag_LogsWarning()
        {
            var node = MakeNode("intro", ("friendly", "trust"));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*unknown_tag.*"));
            StoryProgression.Resolve(node, new[] { "unknown_tag" }, out _);
        }

        [Test]
        public void Resolve_FirstMatchingTagWins_WhenMultipleTagsReturned()
        {
            var node = MakeNode("intro", ("friendly", "trust"), ("hostile", "conflict"));

            var next = StoryProgression.Resolve(node, new[] { "hostile", "friendly" }, out var applied);

            Assert.AreEqual("conflict", next);
            Assert.AreEqual("hostile", applied);
        }

        [Test]
        public void StoryGraph_GetNode_ReturnsMatchingNode()
        {
            var graph = ScriptableObject.CreateInstance<StoryGraph>();
            graph.Nodes = new List<StoryNode> { MakeNode("intro"), MakeNode("trust") };

            var node = graph.GetNode("trust");

            Assert.AreEqual("trust", node.NodeId);
        }
    }
}
