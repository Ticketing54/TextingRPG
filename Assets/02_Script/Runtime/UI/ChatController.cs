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
        private readonly NPCDefinition _npc;
        private readonly string _worldDescription;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        public string NpcDisplayName => _npc.DisplayName;

        public ChatController(
            PlayerState playerState, ILLMProvider provider, NPCDefinition npc, string worldDescription)
        {
            _playerState = playerState;
            _provider = provider;
            _npc = npc;
            _worldDescription = worldDescription;
        }

        public void BeginAdventure()
        {
            var systemPrompt = SystemPromptBuilder.BuildOpening(_npc, _worldDescription);
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
                    _playerState.AppendMessage(_npc.NpcId, narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(_npc.NpcId, npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(_npc.NpcId, response.Summary);
                },
                onError: error => OnError?.Invoke(error)
            );
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded) return;

            ChatMessage playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(_npc.NpcId, playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            _playerState.IncrementTurnCount(_npc.NpcId);
            int turnCount = _playerState.GetTurnCount(_npc.NpcId);
            bool isFinalTurn = turnCount >= MaxTurns;

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var summary = _playerState.GetSummary(_npc.NpcId);
            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription, summary, endingHint);
            var recentHistory = HistoryWindow.TakeRecent(_playerState.GetHistory(_npc.NpcId), MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(_npc.NpcId, narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"));
                        _playerState.AppendMessage(_npc.NpcId, npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    _playerState.SetSummary(_npc.NpcId, response.Summary);
                    EffectApplier.Apply(_playerState, _npc.NpcId, response.Effects);

                    if (isFinalTurn)
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
