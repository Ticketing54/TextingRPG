using UnityEngine;

namespace TextingRPG.Core
{
    [CreateAssetMenu(fileName = "NewEventChanceConfig", menuName = "TextingRPG/Event Chance Config")]
    public class EventChanceConfig : ScriptableObject
    {
        public float NoneWeight = 70f;
        public float GoodWeight = 10f;
        public float BadWeight = 10f;
        public float AllyAppearsWeight = 5f;
        public float DeathWeight = 5f;
    }
}
