using System.Collections.Generic;
using UnityEngine;

namespace TextingRPG.Story
{
    public static class StoryProgression
    {
        public static string Resolve(StoryNode currentNode, IEnumerable<string> tags, out string appliedTag)
        {
            foreach (var tag in tags)
            {
                var transition = currentNode.Transitions.Find(t => t.Tag == tag);
                if (transition != null)
                {
                    appliedTag = tag;
                    return transition.NextNodeId;
                }

                Debug.LogWarning($"StoryProgression: tag '{tag}' not allowed at node '{currentNode.NodeId}', ignored.");
            }

            appliedTag = null;
            return currentNode.NodeId;
        }
    }
}
