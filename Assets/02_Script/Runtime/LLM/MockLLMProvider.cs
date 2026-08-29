using System;
using TextingRPG.Systems;

namespace TextingRPG.LLM
{
    public class MockLLMProvider : ILLMProvider
    {
        public DataManager.StoryOutline NextStoryOutline;
        public string NextOpeningNarration = "";
        public TurnResponse NextTurnResponse;
        public string NextError;
        public ConversationContext LastContext { get; private set; }

        public void GenerateStoryOutline(
            ConversationContext context, Action<DataManager.StoryOutline, string> onSuccess, Action<string> onError)
        {
            LastContext = context;

            if (NextError != null)
            {
                onError?.Invoke(NextError);
                return;
            }

            onSuccess?.Invoke(NextStoryOutline, NextOpeningNarration);
        }

        public void ContinueStory(ConversationContext context, Action<TurnResponse> onSuccess, Action<string> onError)
        {
            LastContext = context;

            if (NextError != null)
            {
                onError?.Invoke(NextError);
                return;
            }

            onSuccess?.Invoke(NextTurnResponse);
        }
    }
}
