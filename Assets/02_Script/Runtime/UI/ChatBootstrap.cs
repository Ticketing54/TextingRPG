using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // Mock 프로바이더로 ChatController <-> ChatBubbleListView 배선이 도는지 확인하기 위한 임시 부트스트랩.
    public class ChatBootstrap : MonoBehaviour
    {
        [SerializeField] ChatBubbleListView chatBubbleListView;
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;

        private ChatController _controller;

        private void Start()
        {
            var playerState = new PlayerState();

            var npc = ScriptableObject.CreateInstance<NPCDefinition>();
            npc.NpcId = "mock_npc";
            npc.DisplayName = "테스트 NPC";
            npc.PersonaDescription = "친절하고 장난기 많은 여관 주인.";

            var storyGraph = ScriptableObject.CreateInstance<StoryGraph>();
            storyGraph.NpcId = npc.NpcId;
            storyGraph.StartNodeId = "start";
            storyGraph.Nodes.Add(new StoryNode { NodeId = "start", SceneDescription = "여관 앞에서 대화 중" });

            var mockProvider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Reply = "안녕하세요! 무엇을 도와드릴까요?" }
            };

            _controller = new ChatController(playerState, mockProvider, npc, "테스트용 세계관 설명", storyGraph);
            chatBubbleListView.Bind(_controller);

            sendButton.onClick.AddListener(SendCurrentInput);
            inputField.onSubmit.AddListener(_ => SendCurrentInput());
        }

        private void SendCurrentInput()
        {
            var text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            _controller.SendPlayerMessage(text);
        }
    }
}
