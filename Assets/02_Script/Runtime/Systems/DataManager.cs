using System;
using System.Collections.Generic;
using TextingRPG.Core;
using UnityEngine;

namespace TextingRPG.Systems
{
    public class DataManager : MonoBehaviour
    {
        public static DataManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

         [Serializable]
        public class StoryOutline
        {
            public string Title;
            public string WorldSetting;
            public string CentralConflict;
            public List<string> KeyCharacters = new List<string>();
            public List<string> KeyEvents = new List<string>();
            public string FinalGoal;
        }

        [Serializable]
        private class Memory
        {
            public List<string> importantFact = new List<string>();
        }

        [Serializable]
        private class Conversation
        {
            public string currentChat;
            public List<ChatMessage> allChat;
        }


        [SerializeField] private StoryOutline storyOutline;
        [SerializeField] private Memory memory;
        [SerializeField] private Conversation conversation;

        public void ApplyStoryOutline(StoryOutline outline) => storyOutline = outline;

        public StoryOutline GetStoryOutline() => storyOutline;

        public void AppendConversationMessage(ChatMessage message)
        {
            GetConversationHistory().Add(message);
        }

        public List<ChatMessage> GetConversationHistory()
        {
            if (conversation == null) conversation = new Conversation();
            if (conversation.allChat == null) conversation.allChat = new List<ChatMessage>();
            return conversation.allChat;
        }

        public void AddImportantFacts(IEnumerable<string> facts)
        {
            GetImportantFacts().AddRange(facts);
        }

        public List<string> GetImportantFacts()
        {
            if (memory == null) memory = new Memory();
            if (memory.importantFact == null) memory.importantFact = new List<string>();
            return memory.importantFact;
        }
    }
}
