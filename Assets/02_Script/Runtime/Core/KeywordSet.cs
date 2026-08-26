using UnityEngine;

namespace TextingRPG.Core
{
    [CreateAssetMenu(fileName = "NewKeywordSet", menuName = "TextingRPG/Keyword Set")]
    public class KeywordSet : ScriptableObject
    {
        public string[] Keywords;
    }
}
