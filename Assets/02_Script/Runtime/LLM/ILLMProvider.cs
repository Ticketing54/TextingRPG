using System;

namespace TextingRPG.LLM
{
    public interface ILLMProvider
    {
        void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError);
    }
}
