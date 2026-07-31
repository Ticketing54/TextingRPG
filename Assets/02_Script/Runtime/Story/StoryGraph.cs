using System.Collections.Generic;
using UnityEngine;

namespace TextingRPG.Story
{
    [CreateAssetMenu(fileName = "NewStoryGraph", menuName = "TextingRPG/Story Graph")]
    public class StoryGraph : ScriptableObject
    {
        public string NpcId;
        public string StartNodeId;
        public List<StoryNode> Nodes = new List<StoryNode>();

        public StoryNode GetNode(string nodeId) => Nodes.Find(n => n.NodeId == nodeId);
    }
}
