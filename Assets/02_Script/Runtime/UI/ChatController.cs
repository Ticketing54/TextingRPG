using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 80;
        private const int MaxTurns = 100;
        private const string OpeningKickoffMessage = "(모험이 시작된다.)";

        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly string _worldDescription;
        private readonly EventChanceConfig _eventConfig;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        public ChatController(
            PlayerState playerState, ILLMProvider provider, string worldDescription, EventChanceConfig eventConfig)
        {
            _playerState = playerState;
            _provider = provider;
            _worldDescription = worldDescription;
            _eventConfig = eventConfig;
        }

        public void BeginAdventure()
        {
            var systemPrompt = SystemPromptBuilder.BuildOpening(_worldDescription);
            // Gemini API는 contents가 빈 배열이면 요청을 거부하므로, 저장되지 않는 시작 트리거 턴 하나를 심어준다.
            var kickoff = new System.Collections.Generic.List<ChatMessage>
            {
                new ChatMessage(ChatSender.Player, OpeningKickoffMessage, DateTime.UtcNow.ToString("o"))
            };
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = kickoff };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(response.Summary);
                },
                onError: error => OnError?.Invoke(error)
            );
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded) return;

            ChatMessage playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            _playerState.IncrementTurnCount();
            int turnCount = _playerState.GetTurnCount();
            bool isFinalTurn = turnCount >= MaxTurns;

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var eventCategory = EventRoller.Roll(_eventConfig);
            var eventHint = EventHintText.For(eventCategory);
            var extraHint = string.IsNullOrEmpty(eventHint) ? endingHint : (endingHint + "\n" + eventHint).Trim();

            var summary = _playerState.GetSummary();
            var systemPrompt = SystemPromptBuilder.Build(_worldDescription, summary, extraHint);
            var recentHistory = HistoryWindow.TakeRecent(_playerState.GetHistory(), MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(response.Summary);
                    EffectApplier.Apply(_playerState, response.Effects);

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
