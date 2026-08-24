using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // ChatController <-> ChatBubbleListView 배선을 실제 Gemini API로 연결하는 부트스트랩.
    // API 키는 프로젝트 루트의 secrets.local.json(커밋 안 됨)에서 읽는다 (코드/에셋에 하드코딩 금지).
    public class ChatBootstrap : MonoBehaviour
    {
        [SerializeField] ChatBubbleListView chatBubbleListView;
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;
        [SerializeField] string geminiModel = "gemini-3.6-flash";
        [SerializeField] float minSecondsBetweenSends = 4f; // Gemini 무료 티어 RPM(분당 15회) 한도에 맞춘 최소 전송 간격
        [SerializeField] NPCDefinition npc;
        [SerializeField] StoryGraph storyGraph;
        [SerializeField] string worldDescription = "중세 판타지 세계, 변방의 작은 마을. 플레이어는 이제 막 마을에 도착한 여행자다.";

        private ChatController _controller;
        private float _lastSendTime = float.NegativeInfinity;

        private void Start()
        {
            var apiKey = LocalSecrets.GetGeminiApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                return;
            }

            var playerState = new PlayerState();

            ILLMProvider provider = new GeminiProvider(apiKey, geminiModel);

            _controller = new ChatController(playerState, provider, npc, worldDescription, storyGraph);
            _controller.OnError += HandleError;
            chatBubbleListView.Bind(_controller);
            chatBubbleListView.OnMessagePlaybackComplete += HandleMessagePlaybackComplete;

            sendButton.onClick.AddListener(SendCurrentInput);
            inputField.onSubmit.AddListener(_ => SendCurrentInput());
        }

        private void SendCurrentInput()
        {
            var text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            sendButton.interactable = false;
            _lastSendTime = Time.time;
            _controller.SendPlayerMessage(text);
        }

        private void HandleMessagePlaybackComplete(ChatMessage message)
        {
            if (message.Sender != ChatSender.Player) EnableSendButtonRespectingCooldown();
        }

        private void HandleError(string error)
        {
            Debug.LogError($"Chat error: {error}");
            EnableSendButtonRespectingCooldown();
        }

        private void EnableSendButtonRespectingCooldown()
        {
            float remaining = minSecondsBetweenSends - (Time.time - _lastSendTime);
            if (remaining <= 0f) sendButton.interactable = true;
            else Invoke(nameof(EnableSendButtonNow), remaining);
        }

        private void EnableSendButtonNow() => sendButton.interactable = true;
    }
}
