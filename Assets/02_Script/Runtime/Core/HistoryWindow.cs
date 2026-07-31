using System.Collections.Generic;

namespace TextingRPG.Core
{
    public static class HistoryWindow
    {
        public static List<ChatMessage> TakeRecent(List<ChatMessage> history, int maxMessages)
        {
            if (history.Count <= maxMessages)
            {
                return new List<ChatMessage>(history);
            }

            return history.GetRange(history.Count - maxMessages, maxMessages);
        }
    }
}
