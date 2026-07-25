using UnityEngine;

namespace TextingRPG.NPC
{
    [CreateAssetMenu(fileName = "NewNPC", menuName = "TextingRPG/NPC Definition")]
    public class NPCDefinition : ScriptableObject
    {
        public string NpcId;
        public string DisplayName;

        [TextArea(3, 10)]
        public string PersonaDescription;
    }
}
