using UnityEngine;

namespace TextingRPG.Core
{
    public static class EventRoller
    {
        public static EventCategory Roll(EventChanceConfig config, float roll01)
        {
            float total = config.NoneWeight + config.GoodWeight + config.BadWeight
                + config.AllyAppearsWeight + config.DeathWeight;
            float scaled = roll01 * total;

            float cumulative = config.NoneWeight;
            if (scaled < cumulative) return EventCategory.None;

            cumulative += config.GoodWeight;
            if (scaled < cumulative) return EventCategory.Good;

            cumulative += config.BadWeight;
            if (scaled < cumulative) return EventCategory.Bad;

            cumulative += config.AllyAppearsWeight;
            if (scaled < cumulative) return EventCategory.AllyAppears;

            return EventCategory.Death;
        }

        public static EventCategory Roll(EventChanceConfig config) => Roll(config, Random.value);
    }
}
