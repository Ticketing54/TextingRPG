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

        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly NPCDefinition _npc;
        private readonly string _worldDescription;
        private readonly StoryGraph _storyGraph;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action<string> OnStoryNodeChanged;

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
            ChatMessage playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(_npc.NpcId, playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            var currentNodeId = _playerState.GetStoryNode(_npc.NpcId) ?? _storyGraph.StartNodeId;
            _playerState.SetStoryNode(_npc.NpcId, currentNodeId);
            var currentNode = _storyGraph.GetNode(currentNodeId);

            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription, currentNode);
            var recentHistory = HistoryWindow.TakeRecent(_playerState.GetHistory(_npc.NpcId), MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var npcMessage = new ChatMessage(ChatSender.Npc, response.Narration, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(_npc.NpcId, npcMessage);
                    EffectApplier.Apply(_playerState, _npc.NpcId, response.Effects);

                    var nextNodeId = StoryProgression.Resolve(currentNode, response.Tags, out _);
                    if (nextNodeId != currentNodeId)
                    {
                        _playerState.SetStoryNode(_npc.NpcId, nextNodeId);
                        OnStoryNodeChanged?.Invoke(nextNodeId);
                    }

                    OnMessageAdded?.Invoke(npcMessage);
                },
                onError: error => OnError?.Invoke(error)
            );
        }
    }
}
