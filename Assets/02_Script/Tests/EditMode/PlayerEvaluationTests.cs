using NUnit.Framework;
using TextingRPG.LLM;
using TextingRPG.Systems;

namespace TextingRPG.Tests
{
    public class PlayerEvaluationTests
    {
        [Test]
        public void Epithet_EmptyTally_IsImprovisingTraveler()
        {
            Assert.AreEqual("즉흥적인 여행자", PlayerEvaluation.Epithet(default));
        }

        [Test]
        public void Epithet_RecklessDominant_IsChargingRecklessOne()
        {
            var t = new PlayerEvaluation.Tally { Safe = 1, Risky = 2, Reckless = 3 };
            Assert.AreEqual("무모한 돌격자", PlayerEvaluation.Epithet(t));
        }

        [Test]
        public void Epithet_SafeMajority_IsCautiousObserver()
        {
            var t = new PlayerEvaluation.Tally { Safe = 5, Risky = 2, Reckless = 1 };
            Assert.AreEqual("신중한 관찰자", PlayerEvaluation.Epithet(t));
        }

        [Test]
        public void Epithet_ManyCritSuccess_IsFatesFavored()
        {
            var t = new PlayerEvaluation.Tally { Safe = 1, Risky = 2, CritSuccess = 3 };
            Assert.AreEqual("운명의 총아", PlayerEvaluation.Epithet(t));
        }

        [Test]
        public void Epithet_ManyCritFail_IsUnluckyWanderer()
        {
            var t = new PlayerEvaluation.Tally { Safe = 1, Risky = 2, CritFail = 3 };
            Assert.AreEqual("불운한 방랑자", PlayerEvaluation.Epithet(t));
        }

        [Test]
        public void Epithet_RiskyDominant_IsCalculatedGambler()
        {
            var t = new PlayerEvaluation.Tally { Safe = 2, Risky = 4, Reckless = 1, CritSuccess = 1, CritFail = 1 };
            Assert.AreEqual("계산된 승부사", PlayerEvaluation.Epithet(t));
        }

        [Test]
        public void CountChoice_And_CountOutcome_Accumulate()
        {
            var t = new PlayerEvaluation.Tally();
            t.CountChoice(ChoiceRisk.Safe);
            t.CountChoice(ChoiceRisk.Risky);
            t.CountChoice(ChoiceRisk.Reckless);
            t.CountOutcome(TextingRPG.Core.DiceOutcome.CriticalSuccess);
            t.CountOutcome(TextingRPG.Core.DiceOutcome.Failure);

            Assert.AreEqual(1, t.Safe);
            Assert.AreEqual(1, t.Risky);
            Assert.AreEqual(1, t.Reckless);
            Assert.AreEqual(1, t.CritSuccess);
            Assert.AreEqual(1, t.Fail);
        }

        [Test]
        public void Summary_IsCentered_MetricsPerLine_EpithetLastAndEnlarged()
        {
            var t = new PlayerEvaluation.Tally { Safe = 2, Risky = 4, Reckless = 7, CritSuccess = 2, CritFail = 1 };
            var s = PlayerEvaluation.Summary(t);

            StringAssert.StartsWith("<align=center>", s);
            StringAssert.EndsWith("</align>", s);
            StringAssert.Contains("\n안전 2\n위험 4\n무모 7\n", s);
            StringAssert.Contains("대성공 2\n대실패 1", s);
            StringAssert.Contains("<size=160%><b>무모한 돌격자</b></size>", s);
            // 칭호가 수치보다 뒤에 온다
            Assert.Less(s.IndexOf("대실패", System.StringComparison.Ordinal),
                        s.IndexOf("무모한 돌격자", System.StringComparison.Ordinal));
        }
    }
}
