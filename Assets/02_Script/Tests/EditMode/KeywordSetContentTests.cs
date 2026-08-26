using NUnit.Framework;
using TextingRPG.Core;
using UnityEditor;

namespace TextingRPG.Tests
{
    public class KeywordSetContentTests
    {
        private const string AssetPath = "Assets/05_Data/Keyword/DefaultKeywordSet.asset";

        [Test]
        public void DefaultKeywordSet_HasExpectedKeywords()
        {
            var keywordSet = AssetDatabase.LoadAssetAtPath<KeywordSet>(AssetPath);

            Assert.IsNotNull(keywordSet, $"{AssetPath} 에셋을 찾을 수 없습니다.");
            CollectionAssert.AreEqual(
                new[] { "안개", "폭풍우", "낡은 지도", "은화", "늑대 울음", "별빛" },
                keywordSet.Keywords);
        }
    }
}
