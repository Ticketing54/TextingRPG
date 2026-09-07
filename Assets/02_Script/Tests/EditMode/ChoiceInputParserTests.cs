using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class ChoiceInputParserTests
    {
        [TestCase("1", 3, 0)]
        [TestCase("2", 3, 1)]
        [TestCase("  3 ", 3, 2)]
        public void Parse_ValidNumber_ReturnsZeroBasedIndex(string text, int count, int expected)
        {
            Assert.AreEqual(expected, ChoiceInputParser.Parse(text, count));
        }

        [TestCase("0", 5)]
        [TestCase("6", 5)]
        [TestCase("문 열어", 5)]
        [TestCase("", 5)]
        [TestCase("   ", 5)]
        [TestCase("2번", 5)]
        public void Parse_NotAChoiceNumber_ReturnsNull(string text, int count)
        {
            Assert.IsNull(ChoiceInputParser.Parse(text, count));
        }

        [Test]
        public void Parse_NoChoicesAvailable_ReturnsNull()
        {
            Assert.IsNull(ChoiceInputParser.Parse("1", 0));
        }
    }
}
