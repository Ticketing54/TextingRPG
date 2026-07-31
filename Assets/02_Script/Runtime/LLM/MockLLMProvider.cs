using System;

namespace TextingRPG.LLM
{
    public class MockLLMProvider : ILLMProvider
    {
        public LLMResponse NextResponse;
        public string NextError;
        public ConversationContext LastContext { get; private set; }

        public void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError)
        {
            LastContext = context;

            if (NextError != null)
            {
                onError?.Invoke(NextError);
                return;
            }

            onSuccess?.Invoke(NextResponse);
        }
    }
}
