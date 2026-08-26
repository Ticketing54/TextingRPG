using System.Collections.Generic;

namespace TextingRPG.Core
{
    public class PlayerState
    {
        private readonly Dictionary<string, float> _stats = new Dictionary<string, float>();
        private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private string _summary = "";
        private int _turnCount;

        public int GetRelationship(string target) =>
            _relationships.TryGetValue(target, out var value) ? value : 0;

        public void SetRelationship(string target, int value) => _relationships[target] = value;

        public float GetStat(string statId) =>
            _stats.TryGetValue(statId, out var value) ? value : 0f;

        public void SetStat(string statId, float value) => _stats[statId] = value;

        public List<ChatMessage> GetHistory() => _history;

        public void AppendMessage(ChatMessage message) => _history.Add(message);

        public string GetSummary() => _summary;

        public void SetSummary(string summary) => _summary = summary;

        public int GetTurnCount() => _turnCount;

        public void IncrementTurnCount() => _turnCount++;
    }
}
