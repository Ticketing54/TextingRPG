using System.Collections.Generic;
using TextingRPG.Core;

namespace TextingRPG.LLM
{
    public class ConversationContext
    {
        public string SystemPrompt;
        public List<ChatMessage> History;
    }
}
