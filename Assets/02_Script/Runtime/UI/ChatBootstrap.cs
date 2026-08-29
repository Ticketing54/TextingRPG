using TextingRPG.Core;
using TextingRPG.LLM;
using UnityEngine;

namespace TextingRPG.UI
{
    // ChatController <-> ChatBubbleListView 배선을 실제 Gemini API로 연결하는 부트스트랩.
    // API 키는 프로젝트 루트의 secrets.local.json(커밋 안 됨)에서 읽는다 (코드/에셋에 하드코딩 금지).
    public class ChatBootstrap : MonoBehaviour
    {
        [SerializeField] ChatBubbleListView chatBubbleListView;
        [SerializeField] ChatInputView chatInputView;
        [SerializeField] string geminiModel = "gemini-3.5-flash-lite"; // thinking 토큰 없이 응답하고, 3.6보다 무료 한도가 넉넉함
        [SerializeField] KeywordIntroPanel keywordIntroPanel;
        [SerializeField] GameObject chatUIRoot;

        private ChatController _controller;
        private string _apiKey;

        private void Start()
        {
            _apiKey = LocalSecrets.GetGeminiApiKey();
            if (string.IsNullOrEmpty(_apiKey))
            {
                return;
            }

            chatUIRoot.SetActive(false);
            keywordIntroPanel.OnKeywordsConfirmed += HandleKeywordsConfirmed;
        }

        private void HandleKeywordsConfirmed(string[] keywords)
        {
            var finalWorldDescription =  "\n\n[이번 모험의 키워드] " + string.Join(", ", keywords);

            ILLMProvider provider = new GeminiProvider(_apiKey, geminiModel);

            _controller = new ChatController(provider, finalWorldDescription);
            _controller.OnError += HandleError;
            _controller.OnConversationEnded += HandleConversationEnded;
            chatBubbleListView.Bind(_controller);
            chatInputView.Bind(_controller);

            chatUIRoot.SetActive(true);
            _controller.BeginAdventure();
        }

        private void HandleError(string error)
        {
            Debug.LogError($"Chat error: {error}");
            chatBubbleListView.NotifyError();
        }

        private void HandleConversationEnded()
        {
            Debug.Log("이야기가 종료되었습니다.");
            chatBubbleListView.Lock();
        }
    }
}
