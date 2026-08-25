using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 80;
        private const int MaxTurns = 100;

        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly NPCDefinition _npc;
        private readonly string _worldDescription;
        private readonly StoryGraph _storyGraph;

        private bool _conversationEnded;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action<string> OnStoryNodeChanged;
        public event Action OnConversationEnded;

        public string NpcDisplayName => _npc.DisplayName;

        public ChatController(
            PlayerState playerState, ILLMProvider provider, NPCDefinition npc,
            string worldDescription, StoryGraph storyGraph)
        {
            _playerState = playerState;
            _provider = provider;
            _npc = npc;
            _worldDescription = worldDescription;
            _storyGraph = storyGraph;
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

            var currentNodeId = _playerState.GetStoryNode(_npc.NpcId) ?? _storyGraph.StartNodeId;
            _playerState.SetStoryNode(_npc.NpcId, currentNodeId);
            var currentNode = _storyGraph.GetNode(currentNodeId);

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription, currentNode, endingHint);
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

                    EffectApplier.Apply(_playerState, _npc.NpcId, response.Effects);

                    var nextNodeId = StoryProgression.Resolve(currentNode, response.Tags, out _);
                    if (nextNodeId != currentNodeId)
                    {
                        _playerState.SetStoryNode(_npc.NpcId, nextNodeId);
                        OnStoryNodeChanged?.Invoke(nextNodeId);
                    }

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
