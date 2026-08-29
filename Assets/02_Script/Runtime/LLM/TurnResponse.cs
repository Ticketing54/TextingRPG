using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class TurnResponse
    {
        public string Narration;
        public string NpcLine = "";
        public List<string> NewFacts = new List<string>();
        public bool IsEnding;
    }
}
