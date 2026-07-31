using System.Collections.Generic;
using System.Linq;

namespace TextingRPG.Story
{
    [System.Serializable]
    public class StoryNode
    {
        public string NodeId;
        public string SceneDescription;
        public List<StoryTransition> Transitions = new List<StoryTransition>();

        public List<string> AllowedTags => Transitions.Select(t => t.Tag).ToList();
    }
}
