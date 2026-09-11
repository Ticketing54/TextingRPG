using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class TurnResponse
    {
        public string Narration;
        public string NpcLine = "";
        public string SpeakerName = "";
        public List<string> NewFacts = new List<string>();
        public List<Choice> Choices = new List<Choice>();
        public bool IsEnding;

        // 플레이어 입력이 이야기와 무관해 이번 턴을 진행하지 않고 무시할 때 true.
        // 이때 Narration에는 짧은 안내 한 문장만 오고 나머지는 비어 있다.
        public bool IsOffTopic;

        // IsEnding일 때의 결말 톤: "good" / "bad" / "bittersweet". 그 외에는 빈 문자열.
        public string EndingTone = "";
    }
}
