using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.LLM;
using TextingRPG.Systems;

namespace TextingRPG.Tests
{
    public class ChoiceListFormatterTests
    {
        private static List<Choice> Sample() => new List<Choice>
        {
            new Choice("뒤로 물러난다", ChoiceRisk.Safe),
            new Choice("문을 연다", ChoiceRisk.Risky),
            new Choice("창문으로 뛰어내린다", ChoiceRisk.Reckless),
        };

        [Test]
        public void Plain_NumbersAndTagsWithNoColorMarkup()
        {
            var text = ChoiceListFormatter.Plain(Sample());

            Assert.AreEqual(
                "1. [안전] 뒤로 물러난다\n2. [위험] 문을 연다\n3. [무모] 창문으로 뛰어내린다",
                text);
            StringAssert.DoesNotContain("<color", text);
        }

        [Test]
        public void Colored_WrapsOnlyTagLabelPerRisk()
        {
            var text = ChoiceListFormatter.Colored(Sample());

            StringAssert.Contains("<color=#2E9E5B>[안전]</color> 뒤로 물러난다", text);
            StringAssert.Contains("<color=#C88A1E>[위험]</color> 문을 연다", text);
            StringAssert.Contains("<color=#D33A3A>[무모]</color> 창문으로 뛰어내린다", text);
        }

        [Test]
        public void Colored_DoesNotColorBodyOrNumbers()
        {
            var text = ChoiceListFormatter.Colored(Sample());

            // 색 태그를 모두 걷어내면 Plain과 같아야 한다 (번호·본문은 색 밖).
            var stripped = System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", "");
            Assert.AreEqual(ChoiceListFormatter.Plain(Sample()), stripped);
        }

        [Test]
        public void EmptyOrNull_ReturnsEmptyString()
        {
            Assert.AreEqual("", ChoiceListFormatter.Plain(new List<Choice>()));
            Assert.AreEqual("", ChoiceListFormatter.Colored(new List<Choice>()));
            Assert.AreEqual("", ChoiceListFormatter.Plain(null));
            Assert.AreEqual("", ChoiceListFormatter.Colored(null));
        }
    }
}
