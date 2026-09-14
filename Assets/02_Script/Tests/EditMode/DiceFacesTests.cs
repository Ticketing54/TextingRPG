using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class DiceFacesTests
    {
        [TestCase(1, "⚀")]
        [TestCase(2, "⚁")]
        [TestCase(3, "⚂")]
        [TestCase(4, "⚃")]
        [TestCase(5, "⚄")]
        [TestCase(6, "⚅")]
        public void Glyph_MapsFaceValueToGlyph(int value, string expected)
        {
            Assert.AreEqual(expected, DiceFaces.Glyph(value));
        }

        [Test]
        public void Glyph_OutOfRange_Clamps()
        {
            Assert.AreEqual("⚀", DiceFaces.Glyph(0));
            Assert.AreEqual("⚀", DiceFaces.Glyph(-3));
            Assert.AreEqual("⚅", DiceFaces.Glyph(7));
            Assert.AreEqual("⚅", DiceFaces.Glyph(99));
        }
    }
}
