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

        public ChatMessage(ChatSender sender, string text, string timestamp, string senderName = "")
        {
            Sender = sender;
            Text = text;
            Timestamp = timestamp;
            SenderName = senderName;
        }
    }
}
