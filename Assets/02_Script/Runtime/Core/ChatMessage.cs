namespace TextingRPG.Core
{
    public enum ChatSender
    {
        Player,
        Npc,
        Narration
    }

    [System.Serializable]
    public class ChatMessage
    {
        public ChatSender Sender;
        public string Text;
        public string Timestamp;
        public string SenderName;

        // 화면 표시용 텍스트. 비어 있으면 Text를 그대로 표시한다.
        // 프롬프트와 대화 히스토리는 언제나 Text를 쓴다 — DisplayText는 순수 표시용(예: 선택지 태그 색).
        public string DisplayText;

        public ChatMessage(ChatSender sender, string text, string timestamp, string senderName = "")
        {
            Sender = sender;
            Text = text;
            Timestamp = timestamp;
            SenderName = senderName;
        }
    }
}
