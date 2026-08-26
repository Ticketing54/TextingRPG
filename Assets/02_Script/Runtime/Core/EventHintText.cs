namespace TextingRPG.Core
{
    public static class EventHintText
    {
        public static string For(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Good:
                    return "이번 턴, 플레이어에게 뜻밖의 좋은 일이 일어날 후보다. 지금까지 " +
                        "맥락상 자연스럽다면 긍정적인 사건을 반영해라. 부자연스러우면 무시해라.";

                case EventCategory.Bad:
                    return "이번 턴, 플레이어에게 좋지 않은 일이 일어날 후보다. 지금까지 맥락상 " +
                        "자연스럽다면 반영해라. 너무 가혹하거나 부자연스러우면 무시해라.";

                case EventCategory.AllyAppears:
                    return "이번 턴, 플레이어를 도와줄 새로운 인물이 등장할 후보다. 지금까지 " +
                        "맥락상 자연스럽다면 새로운 조력자를 등장시켜라. 부자연스러우면 무시해라.";

                case EventCategory.Death:
                    return "이번 턴, 플레이어가 즉사할 수도 있는 결정적 위험이 닥칠 후보다. " +
                        "지금까지 맥락상 정말 자연스럽다면 플레이어가 사망하는 것으로 반영하고 " +
                        "isEnding을 true로 보고해라. 조금이라도 부자연스럽거나 뜬금없다면 " +
                        "무시하고 평범하게 진행해라.";

                default:
                    return "";
            }
        }
    }
}
