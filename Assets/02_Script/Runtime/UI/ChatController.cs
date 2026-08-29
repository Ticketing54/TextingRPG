using System;
using System.Collections.Generic;
using System.Linq;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.Systems;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 80;
        private const int MaxTurns = 100;
        private const string OpeningKickoffMessage = "(모험이 시작된다.)";

        private readonly ILLMProvider _provider;
        private readonly string _worldDescription;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        public ChatController(ILLMProvider provider, string worldDescription)
        {
            _provider = provider;
            _worldDescription = worldDescription;
        }

        public void BeginAdventure()
        {
            var systemPrompt = PromptBuilder.BuildStoryOutlinePrompt(_worldDescription);
            // Gemini API는 contents가 빈 배열이면 요청을 거부하므로, 저장되지 않는 시작 트리거 턴 하나를 심어준다.
            var kickoff = new List<ChatMessage>
            {
                new ChatMessage(ChatSender.Player, OpeningKickoffMessage, DateTime.UtcNow.ToString("o"))
            };
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = kickoff };

            _provider.GenerateStoryOutline(
                context,
                onSuccess: (outline, openingNarration) =>
                {
                    DataManager.Instance.ApplyStoryOutline(outline);

                    var narrationMessage = new ChatMessage(ChatSender.Narration, openingNarration, DateTime.UtcNow.ToString("o"));
                    DataManager.Instance.AppendConversationMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    Debug.Log($"StoryOutline applied: {outline.Title}");
                },
                onError: error => OnError?.Invoke(error)
            );
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded) return;

            var playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            DataManager.Instance.AppendConversationMessage(playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            var fullHistory = DataManager.Instance.GetConversationHistory();
            int turnCount = fullHistory.Count(m => m.Sender == ChatSender.Player);
            bool isFinalTurn = turnCount >= MaxTurns;

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var outline = DataManager.Instance.GetStoryOutline();
            var facts = DataManager.Instance.GetImportantFacts();
            var systemPrompt = PromptBuilder.BuildTurnPrompt(outline, facts, endingHint);
            var recentHistory = HistoryWindow.TakeRecent(fullHistory, MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.ContinueStory(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    DataManager.Instance.AppendConversationMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        DataManager.Instance.AppendConversationMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    DataManager.Instance.AddImportantFacts(response.NewFacts);

                    if (isFinalTurn || response.IsEnding)
                    {
                        _conversationEnded = true;
                        OnConversationEnded?.Invoke();
                    }
                },
                onError: error => OnError?.Invoke(error)
            );
        }
    }
}
