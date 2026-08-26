using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class EventHintTextTests
    {
        [Test]
        public void For_None_ReturnsEmptyString()
        {
            Assert.AreEqual("", EventHintText.For(EventCategory.None));
        }

        [Test]
        public void For_Good_MentionsPositiveEvent()
        {
            StringAssert.Contains("좋은 일", EventHintText.For(EventCategory.Good));
        }

        [Test]
        public void For_Bad_MentionsNegativeEvent()
        {
            StringAssert.Contains("좋지 않은 일", EventHintText.For(EventCategory.Bad));
        }

        [Test]
        public void For_AllyAppears_MentionsNewAlly()
        {
            StringAssert.Contains("도와줄 새로운 인물", EventHintText.For(EventCategory.AllyAppears));
        }

        [Test]
        public void For_Death_MentionsIsEndingFlag()
        {
            StringAssert.Contains("isEnding", EventHintText.For(EventCategory.Death));
        }

        [Test]
        public void For_AllNonNoneCategories_MentionIgnoringIfUnnatural()
        {
            StringAssert.Contains("무시", EventHintText.For(EventCategory.Good));
            StringAssert.Contains("무시", EventHintText.For(EventCategory.Bad));
            StringAssert.Contains("무시", EventHintText.For(EventCategory.AllyAppears));
        }
    }
}
