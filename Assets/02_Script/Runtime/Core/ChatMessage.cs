namespace TextingRPG.Core
{
    public enum ChatSender
    {
        Player,
        Npc
    }

    [System.Serializable]
    public class ChatMessage
    {
        public ChatSender Sender;
        public string Text;
        public string Timestamp;

        public ChatMessage(ChatSender sender, string text, string timestamp)
        {
            Sender = sender;
            Text = text;
            Timestamp = timestamp;
        }
    }
}
