using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using UnityEngine.TestTools;

namespace TextingRPG.Tests
{
    public class EffectApplierTests
    {
        [Test]
        public void Apply_RelationshipEffect_IncreasesRelationship()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 2f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(2, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_RelationshipEffect_ClampsLargeDelta()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = 999f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(3, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_RelationshipEffect_ClampsLargeNegativeDelta()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "relationship", Target = "npc_a", Delta = -999f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(-3, state.GetRelationship("npc_a"));
        }

        [Test]
        public void Apply_StatEffect_UpdatesNamedStat()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "stat", Target = "courage", Delta = 1.5f }
            };

            EffectApplier.Apply(state, effects);

            Assert.AreEqual(1.5f, state.GetStat("courage"));
        }

        [Test]
        public void Apply_UnknownEffectType_IsIgnoredAndLogsWarning()
        {
            var state = new PlayerState();
            var effects = new List<LLMEffect>
            {
                new LLMEffect { Type = "teleport", Target = "npc_a", Delta = 1f }
            };

            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex(".*teleport.*"));
            EffectApplier.Apply(state, effects);

            Assert.AreEqual(0, state.GetRelationship("npc_a"));
        }
    }
}
