using System;
using TextingRPG.Systems;

namespace TextingRPG.LLM
{
    public interface ILLMProvider
    {
        void GenerateStoryOutline(
            ConversationContext context, Action<DataManager.StoryOutline, string> onSuccess, Action<string> onError);

        void ContinueStory(ConversationContext context, Action<TurnResponse> onSuccess, Action<string> onError);
    }
}
