using System.Collections.Generic;
using System.Text;
using TextingRPG.LLM;

namespace TextingRPG.Systems
{
    // 선택지 목록을 채팅에 보여줄 문자열로 만든다.
    // Plain은 대화 히스토리/프롬프트에 저장하는 평문, Colored는 태그 라벨에만 색을 입힌 표시용.
    public static class ChoiceListFormatter
    {
        // 태그 라벨 색 (TMP 리치텍스트 hex). 배경 보고 조정하기 쉽게 여기 모아둔다.
        private const string SafeColor = "#2E9E5B";     // 초록
        private const string RiskyColor = "#C88A1E";    // 진노랑/골드 — 순수 노랑은 밝은 배경에서 안 보임
        private const string RecklessColor = "#D33A3A"; // 빨강

        public static string Plain(IReadOnlyList<Choice> choices)
        {
            return Build(choices, colored: false);
        }

        public static string Colored(IReadOnlyList<Choice> choices)
        {
            return Build(choices, colored: true);
        }

        private static string Build(IReadOnlyList<Choice> choices, bool colored)
        {
            if (choices == null || choices.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < choices.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var label = Label(choices[i].Risk);
                if (colored) label = $"<color={Color(choices[i].Risk)}>{label}</color>";
                sb.Append($"{i + 1}. {label} {choices[i].Text}");
            }
            return sb.ToString();
        }

        private static string Label(ChoiceRisk risk)
        {
            switch (risk)
            {
                case ChoiceRisk.Risky: return "[위험]";
                case ChoiceRisk.Reckless: return "[무모]";
                default: return "[안전]";
            }
        }

        private static string Color(ChoiceRisk risk)
        {
            switch (risk)
            {
                case ChoiceRisk.Risky: return RiskyColor;
                case ChoiceRisk.Reckless: return RecklessColor;
                default: return SafeColor;
            }
        }
    }
}
