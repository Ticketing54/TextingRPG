using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.Systems;

namespace TextingRPG.Tests
{
    public class DiceDirectionTextTests
    {
        private static readonly DiceOutcome[] AllOutcomes =
        {
            DiceOutcome.CriticalFailure, DiceOutcome.Failure, DiceOutcome.Partial,
            DiceOutcome.Success, DiceOutcome.CriticalSuccess
        };

        [Test]
        public void ForRisky_EveryOutcomeHasDistinctNonEmptyText()
        {
            var seen = new HashSet<string>();
            foreach (var outcome in AllOutcomes)
            {
                var text = DiceDirectionText.ForRisky(outcome);
                Assert.IsNotEmpty(text);
                Assert.IsTrue(seen.Add(text), $"'{outcome}' 문장이 다른 등급과 중복됨");
            }
        }

        [Test]
        public void ForReckless_EveryOutcomeHasDistinctNonEmptyText()
        {
            var seen = new HashSet<string>();
            foreach (var outcome in AllOutcomes)
            {
                var text = DiceDirectionText.ForReckless(outcome);
                Assert.IsNotEmpty(text);
                Assert.IsTrue(seen.Add(text), $"'{outcome}' 무모 문장이 다른 등급과 중복됨");
            }
        }

        [Test]
        public void ForReckless_DiffersFromForRisky()
        {
            foreach (var outcome in AllOutcomes)
                Assert.AreNotEqual(DiceDirectionText.ForRisky(outcome), DiceDirectionText.ForReckless(outcome));
        }

        [Test]
        public void ForSafe_IsNonEmptyAndDiffersFromRiskyAndReckless()
        {
            var safe = DiceDirectionText.ForSafe();
            Assert.IsNotEmpty(safe);
            foreach (var outcome in AllOutcomes)
            {
                Assert.AreNotEqual(safe, DiceDirectionText.ForRisky(outcome));
                Assert.AreNotEqual(safe, DiceDirectionText.ForReckless(outcome));
            }
        }

        [TestCase(DiceOutcome.CriticalFailure, "대실패")]
        [TestCase(DiceOutcome.Failure, "실패")]
        [TestCase(DiceOutcome.Partial, "부분 성공")]
        [TestCase(DiceOutcome.Success, "성공")]
        [TestCase(DiceOutcome.CriticalSuccess, "대성공")]
        public void ShortLabel_MapsOutcome(DiceOutcome outcome, string expected)
        {
            Assert.AreEqual(expected, DiceDirectionText.ShortLabel(outcome));
        }
    }
}
