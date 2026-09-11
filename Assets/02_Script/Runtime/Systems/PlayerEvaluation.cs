using TextingRPG.Core;
using TextingRPG.LLM;

namespace TextingRPG.Systems
{
    // 엔딩 시 플레이 스타일을 칭호 + 수치 한 줄로 요약한다. 코드 규칙 기반, API 호출 없음.
    // 칭호·임계값은 아래 Epithet에서 바로 수정하면 된다.
    public static class PlayerEvaluation
    {
        // 플레이 중 ChatController가 누적한다.
        public struct Tally
        {
            public int Safe, Risky, Reckless;
            public int CritFail, Fail, Partial, Success, CritSuccess;

            public void CountChoice(ChoiceRisk risk)
            {
                switch (risk)
                {
                    case ChoiceRisk.Risky: Risky++; break;
                    case ChoiceRisk.Reckless: Reckless++; break;
                    default: Safe++; break;
                }
            }

            public void CountOutcome(DiceOutcome outcome)
            {
                switch (outcome)
                {
                    case DiceOutcome.CriticalFailure: CritFail++; break;
                    case DiceOutcome.Failure: Fail++; break;
                    case DiceOutcome.Partial: Partial++; break;
                    case DiceOutcome.Success: Success++; break;
                    case DiceOutcome.CriticalSuccess: CritSuccess++; break;
                }
            }
        }

        public static string Epithet(Tally t)
        {
            if (t.Reckless >= t.Risky && t.Reckless > t.Safe) return "무모한 돌격자";
            if (t.Safe > t.Risky + t.Reckless) return "신중한 관찰자";
            if (t.CritSuccess >= 3) return "운명의 총아";
            if (t.CritFail >= 3) return "불운한 방랑자";
            if (t.Risky >= t.Safe && t.Risky >= t.Reckless && t.Risky > 0) return "계산된 승부사";
            return "즉흥적인 여행자";
        }

        // 엔딩 화면에 그대로 넣는 문자열. 가운데 정렬, 항목별 한 줄씩, 총평(칭호)은 맨 아래에 크게.
        public static string Summary(Tally t) =>
            "<align=center>━ 플레이어 평가 ━\n\n" +
            $"안전 {t.Safe}\n위험 {t.Risky}\n무모 {t.Reckless}\n\n" +
            $"대성공 {t.CritSuccess}\n대실패 {t.CritFail}\n\n" +
            $"<size=160%><b>{Epithet(t)}</b></size></align>";
    }
}
