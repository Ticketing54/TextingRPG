using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class DiceRollTests
    {
        [TestCase(2, DiceOutcome.CriticalFailure)]
        [TestCase(3, DiceOutcome.Failure)]
        [TestCase(6, DiceOutcome.Failure)]
        [TestCase(7, DiceOutcome.Partial)]
        [TestCase(9, DiceOutcome.Partial)]
        [TestCase(10, DiceOutcome.Success)]
        [TestCase(11, DiceOutcome.Success)]
        [TestCase(12, DiceOutcome.CriticalSuccess)]
        public void Bucket_Risky_MapsTotalToOutcome(int total, DiceOutcome expected)
        {
            Assert.AreEqual(expected, DiceRoll.Bucket(total));
        }

        [TestCase(2, DiceOutcome.CriticalFailure)]
        [TestCase(3, DiceOutcome.CriticalFailure)]
        [TestCase(4, DiceOutcome.Failure)]
        [TestCase(6, DiceOutcome.Failure)]
        [TestCase(7, DiceOutcome.Partial)]
        [TestCase(8, DiceOutcome.Partial)]
        [TestCase(9, DiceOutcome.Success)]
        [TestCase(10, DiceOutcome.Success)]
        [TestCase(11, DiceOutcome.CriticalSuccess)]
        [TestCase(12, DiceOutcome.CriticalSuccess)]
        public void Bucket_Reckless_HasWiderCriticalBands(int total, DiceOutcome expected)
        {
            Assert.AreEqual(expected, DiceRoll.Bucket(total, reckless: true));
        }

        [Test]
        public void Roll_EachDieIsOneToSix_TotalIsTwoToTwelve()
        {
            for (int i = 0; i < 200; i++)
            {
                var roll = DiceRoll.Roll();
                Assert.GreaterOrEqual(roll.A, 1);
                Assert.LessOrEqual(roll.A, 6);
                Assert.GreaterOrEqual(roll.B, 1);
                Assert.LessOrEqual(roll.B, 6);
                Assert.AreEqual(roll.A + roll.B, roll.Total);
            }
        }
    }
}
