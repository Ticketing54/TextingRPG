namespace TextingRPG.Core
{
    // 플레이어가 입력창에 친 텍스트가 선택지 번호인지(1~choiceCount) 판별한다.
    // 선택지 번호면 0-based 인덱스를, 아니면 null(자유 입력)을 반환.
    public static class ChoiceInputParser
    {
        public static int? Parse(string text, int choiceCount)
        {
            if (choiceCount <= 0 || string.IsNullOrWhiteSpace(text)) return null;
            if (!int.TryParse(text.Trim(), out int number)) return null;
            if (number < 1 || number > choiceCount) return null;
            return number - 1;
        }
    }
}
