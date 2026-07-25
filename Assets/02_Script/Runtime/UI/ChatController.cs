using System;
using TextingRPG.Core;
using TextingRPG.LLM;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private readonly PlayerState _playerState;
        private readonly ILLMProvider _provider;
        private readonly string _npcId;
        private readonly string _systemPrompt;

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;

        public ChatController(PlayerState playerState, ILLMProvider provider, string npcId, string systemPrompt)
        {
            _playerState = playerState;
            _provider = provider;
            _npcId = npcId;
            _systemPrompt = systemPrompt;
        }

        public void SendPlayerMessage(string text)
        {
            var playerMessage = new ChatMessage(ChatSender.Player, text, DateTime.UtcNow.ToString("o"));
            _playerState.AppendMessage(_npcId, playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            var context = new ConversationContext
            {
                SystemPrompt = _systemPrompt,
                History = new System.Collections.Generic.List<ChatMessage>(_playerState.GetHistory(_npcId))
            };

            _provider.SendMessage(
                context,
                onSuccess: response =>
                {
                    var npcMessage = new ChatMessage(ChatSender.Npc, response.Reply, DateTime.UtcNow.ToString("o"));
                    _playerState.AppendMessage(_npcId, npcMessage);
                    EffectApplier.Apply(_playerState, _npcId, response.Effects);
                    OnMessageAdded?.Invoke(npcMessage);
                },
                onError: error =>
                {
                    _playerState.GetHistory(_npcId).Remove(playerMessage);
                    OnError?.Invoke(error);
                }
            );
        }
    }
}
