using TextingRPG.Core;

namespace TextingRPG.Systems
{
    // 주사위 등급 / 안전 선택을 프롬프트에 넣을 "방향 문장"으로 번역한다.
    // LLM에게는 숫자가 아니라 이 문장만 전달한다.
    public static class DiceDirectionText
    {
        public static string ForRisky(DiceOutcome outcome)
        {
            switch (outcome)
            {
                case DiceOutcome.CriticalFailure:
                    return "플레이어의 이번 시도는 완전히 어긋나 심각한 문제나 큰 대가가 생긴다. " +
                           "단, 이야기가 여기서 끝나버리지는 않게 한다.";
                case DiceOutcome.Failure:
                    return "플레이어의 이번 시도는 뜻대로 되지 않거나 예상치 못한 문제가 생긴다. " +
                           "단, 이야기가 막다른 길로 가지는 않게 한다.";
                case DiceOutcome.Partial:
                    return "플레이어의 이번 시도는 어느 정도 통하지만, 대가나 새로운 복잡함이 따른다.";
                case DiceOutcome.Success:
                    return "플레이어의 이번 시도는 순조롭게 통한다.";
                default: // CriticalSuccess
                    return "플레이어의 이번 시도는 기대 이상으로 완벽하게 통하고, 뜻밖의 이득까지 따라온다.";
            }
        }

        // 무모한 선택 — 위험보다 성공·실패의 폭이 더 크다.
        public static string ForReckless(DiceOutcome outcome)
        {
            switch (outcome)
            {
                case DiceOutcome.CriticalFailure:
                    return "플레이어의 무모한 시도는 완전히 역효과를 내 돌이키기 힘든 피해나 큰 대가가 생긴다. " +
                           "단, 이야기가 여기서 끝나버리지는 않게 한다.";
                case DiceOutcome.Failure:
                    return "플레이어의 무모한 시도는 실패하고, 그 대가가 평소보다 크다.";
                case DiceOutcome.Partial:
                    return "플레이어의 무모한 시도는 통하긴 하지만 상당한 대가나 후폭풍이 따른다.";
                case DiceOutcome.Success:
                    return "플레이어의 무모한 시도가 통해서 큰 성과를 얻는다.";
                default: // CriticalSuccess
                    return "플레이어의 무모한 시도가 완벽하게 통해, 판을 뒤집을 만한 큰 이득을 얻는다.";
            }
        }

        public static string ForSafe()
        {
            return "플레이어는 안전한 선택을 했다. 무리 없이 진행되지만 큰 이득이나 진전은 없다.";
        }

        // 플레이어에게 보여줄 짧은 등급 라벨.
        public static string ShortLabel(DiceOutcome outcome)
        {
            switch (outcome)
            {
                case DiceOutcome.CriticalFailure: return "대실패";
                case DiceOutcome.Failure: return "실패";
                case DiceOutcome.Partial: return "부분 성공";
                case DiceOutcome.Success: return "성공";
                default: return "대성공";
            }
        }
    }
}
