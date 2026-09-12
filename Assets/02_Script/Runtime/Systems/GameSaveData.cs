using System.Collections.Generic;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.UI;

namespace TextingRPG.Systems
{
    // 게임 1판(진행 중 또는 완료)을 저장/복원하는 데 필요한 전부.
    [System.Serializable]
    public class GameSaveData
    {
        public string PlayerName;
        public DataManager.StoryOutline Outline;
        public List<string> ImportantFacts = new List<string>();
        public List<ChatMessage> History = new List<ChatMessage>();
        public List<Choice> LastChoices = new List<Choice>();
        public ChatController.Tally Tally;

        // 진행 중이면 빈 문자열, 완료되면 "good"/"bad"/"bittersweet".
        public string EndingTone = "";

        public string SavedAtIso;
    }

    // 히스토리 목록 화면에 보여줄 가벼운 항목 (전체 대화는 안 담음).
    [System.Serializable]
    public class HistoryEntry
    {
        public string Id;
        public string Title;
        public string EndingTone;
        public string SavedAtIso;
    }

    [System.Serializable]
    public class HistoryIndex
    {
        public List<HistoryEntry> Entries = new List<HistoryEntry>();
    }
}
