using NUnit.Framework;
using TextingRPG.Core;
using UnityEngine;

namespace TextingRPG.Tests
{
    public class EventRollerTests
    {
        private static EventChanceConfig MakeConfig()
        {
            var config = ScriptableObject.CreateInstance<EventChanceConfig>();
            config.NoneWeight = 70f;
            config.GoodWeight = 10f;
            config.BadWeight = 10f;
            config.AllyAppearsWeight = 5f;
            config.DeathWeight = 5f;
            return config;
        }

        [Test]
        public void Roll_LowValue_ReturnsNone()
        {
            Assert.AreEqual(EventCategory.None, EventRoller.Roll(MakeConfig(), 0f));
        }

        [Test]
        public void Roll_JustBelowNoneBoundary_ReturnsNone()
        {
            Assert.AreEqual(EventCategory.None, EventRoller.Roll(MakeConfig(), 0.699f));
        }

        [Test]
        public void Roll_AtNoneBoundary_ReturnsGood()
        {
            Assert.AreEqual(EventCategory.Good, EventRoller.Roll(MakeConfig(), 0.70f));
        }

        [Test]
        public void Roll_JustBelowGoodBoundary_ReturnsGood()
        {
            Assert.AreEqual(EventCategory.Good, EventRoller.Roll(MakeConfig(), 0.79f));
        }

        [Test]
        public void Roll_AtGoodBoundary_ReturnsBad()
        {
            Assert.AreEqual(EventCategory.Bad, EventRoller.Roll(MakeConfig(), 0.80f));
        }

        [Test]
        public void Roll_AtBadBoundary_ReturnsAllyAppears()
        {
            Assert.AreEqual(EventCategory.AllyAppears, EventRoller.Roll(MakeConfig(), 0.90f));
        }

        [Test]
        public void Roll_AtAllyAppearsBoundary_ReturnsDeath()
        {
            Assert.AreEqual(EventCategory.Death, EventRoller.Roll(MakeConfig(), 0.95f));
        }

        [Test]
        public void Roll_HighValue_ReturnsDeath()
        {
            Assert.AreEqual(EventCategory.Death, EventRoller.Roll(MakeConfig(), 0.999f));
        }

        [Test]
        public void Roll_WithoutExplicitRollValue_ReturnsValidCategory()
        {
            var category = EventRoller.Roll(MakeConfig());
            Assert.IsTrue(System.Enum.IsDefined(typeof(EventCategory), category));
        }
    }
}
