using UnityEngine;

namespace TextingRPG.Core
{
    // 주사위 눈 값(1~6)을 글리프 문자(⚀~⚅, U+2680–U+2685)로 바꾼다.
    // 주사위 눈 글리프의 표준 소스. DiceRollAnimation은 씬/프리팹에 직렬화된 faces 값을
    // 그대로 쓰므로 이 클래스와는 별개다 (같은 글리프 집합을 쓴다는 전제).
    public static class DiceFaces
    {
        private static readonly string[] Glyphs = { "⚀", "⚁", "⚂", "⚃", "⚄", "⚅" };

        public static string Glyph(int value)
        {
            int index = Mathf.Clamp(value - 1, 0, Glyphs.Length - 1);
            return Glyphs[index];
        }
    }
}
