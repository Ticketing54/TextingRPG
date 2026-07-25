using System.Collections.Generic;

namespace TextingRPG.Core
{
    public class PlayerState
    {
        private readonly Dictionary<string, float> _stats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private readonly Dictionary<string, List<ChatMessage>> _conversationHistories =
            new Dictionary<string, List<ChatMessage>>();

        public int GetRelationship(string npcId) =>
            _relationships.TryGetValue(npcId, out var value) ? value : 0;

        public void SetRelationship(string npcId, int value) => _relationships[npcId] = value;

        public float GetStat(string statId) =>
            _stats.TryGetValue(statId, out var value) ? value : 0f;

        public void SetStat(string statId, float value) => _stats[statId] = value;

        public List<ChatMessage> GetHistory(string npcId)
        {
            if (!_conversationHistories.TryGetValue(npcId, out var history))
            {
                history = new List<ChatMessage>();
                _conversationHistories[npcId] = history;
            }
            return history;
        }

        public void AppendMessage(string npcId, ChatMessage message)
        {
            GetHistory(npcId).Add(message);
        }
    }
}
