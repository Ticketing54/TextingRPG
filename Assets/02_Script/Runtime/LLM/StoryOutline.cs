using System.Collections.Generic;

namespace TextingRPG.LLM
{
    [System.Serializable]
    public class StoryOutline
    {
        public string Title;
        public string WorldSetting;
        public List<string> KeyCharacters = new List<string>();
        public List<string> KeyEvents = new List<string>();
    }
}
