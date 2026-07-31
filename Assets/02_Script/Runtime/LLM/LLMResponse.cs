using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class LLMResponse
    {
        public string Reply;
        public List<string> Tags = new List<string>();
        public List<LLMEffect> Effects = new List<LLMEffect>();
    }
}
