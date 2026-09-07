namespace TextingRPG.LLM
{
    public enum ChoiceRisk
    {
        Safe,     // 안전 — 판정 없이 성공하지만 정체
        Risky,    // 위험 — 2d6 판정
        Reckless  // 무모 — 2d6 판정, 성패가 더 크게 갈림
    }

    // LLM이 매 턴 제시하는 행동 선택지 하나.
    [System.Serializable]
    public class Choice
    {
        public string Text;
        public ChoiceRisk Risk;

        public Choice() { }

        public Choice(string text, ChoiceRisk risk)
        {
            Text = text;
            Risk = risk;
        }
    }
}
