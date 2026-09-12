using NUnit.Framework;
using TextingRPG.UI;

namespace TextingRPG.Tests
{
    public class FloatingMenuLayoutTests
    {
        [TestCase(-100f, 800f, 40f, 16f, -344f)]
        [TestCase(-1f, 800f, 40f, 16f, -344f)]
        public void SnapTargetX_LeftOfCenter_ReturnsNegativeEdge(float currentX, float parentWidth, float radius, float margin, float expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.SnapTargetX(currentX, parentWidth, radius, margin), 0.001f);
        }

        [TestCase(0f, 800f, 40f, 16f, 344f)]
        [TestCase(250f, 800f, 40f, 16f, 344f)]
        public void SnapTargetX_AtOrRightOfCenter_ReturnsPositiveEdge(float currentX, float parentWidth, float radius, float margin, float expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.SnapTargetX(currentX, parentWidth, radius, margin), 0.001f);
        }

        [TestCase(-50f, 1)]
        [TestCase(-1f, 1)]
        public void ExpandDirectionSign_LeftOfCenter_ReturnsPositiveOne(float currentX, int expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.ExpandDirectionSign(currentX));
        }

        [TestCase(0f, -1)]
        [TestCase(50f, -1)]
        public void ExpandDirectionSign_AtOrRightOfCenter_ReturnsNegativeOne(float currentX, int expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.ExpandDirectionSign(currentX));
        }
    }
}
