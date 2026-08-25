using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class LLMResponse
    {
        public string Narration;
        public string NpcLine = "";
        public string Summary = "";
        public List<LLMEffect> Effects = new List<LLMEffect>();
    }
}
