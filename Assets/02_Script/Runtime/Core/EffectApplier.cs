using System.Collections.Generic;
using TextingRPG.LLM;
using UnityEngine;

namespace TextingRPG.Core
{
    public static class EffectApplier
    {
        private const float MaxAbsDelta = 3f;

        public static void Apply(PlayerState state, string npcId, IEnumerable<LLMEffect> effects)
        {
            foreach (var effect in effects)
            {
                float clampedDelta = Mathf.Clamp(effect.Delta, -MaxAbsDelta, MaxAbsDelta);

                switch (effect.Type)
                {
                    case "relationship":
                        int current = state.GetRelationship(npcId);
                        state.SetRelationship(npcId, current + Mathf.RoundToInt(clampedDelta));
                        break;

                    case "stat":
                        float currentStat = state.GetStat(effect.Target);
                        state.SetStat(effect.Target, currentStat + clampedDelta);
                        break;

                    default:
                        Debug.LogWarning($"EffectApplier: unknown effect type '{effect.Type}', ignored.");
                        break;
                }
            }
        }
    }
}
