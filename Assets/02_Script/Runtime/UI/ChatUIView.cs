using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class ChatUIView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;
        [SerializeField] private Transform _messageContainer;
        [SerializeField] private ChatMessageView _messagePrefab;
        [SerializeField] private TMP_Text _errorText;

        private ChatController _controller;

        public void Init(ChatController controller)
        {
            _controller = controller;
            _controller.OnMessageAdded += HandleMessageAdded;
            _controller.OnError += HandleError;

            _sendButton.onClick.AddListener(HandleSendClicked);
        }

        private void OnDestroy()
        {
            if (_controller == null)
            {
                return;
            }
            _controller.OnMessageAdded -= HandleMessageAdded;
            _controller.OnError -= HandleError;
        }

        private void HandleSendClicked()
        {
            var text = _inputField.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _inputField.text = string.Empty;
            _sendButton.interactable = false;
            _controller.SendPlayerMessage(text);
        }

        private void HandleMessageAdded(ChatMessage message)
        {
            var view = Instantiate(_messagePrefab, _messageContainer);
            view.Bind(message);

            if (message.Sender == ChatSender.Npc)
            {
                _sendButton.interactable = true;
            }
        }

        private void HandleError(string error)
        {
            if (_errorText != null)
            {
                _errorText.text = $"오류: {error}";
            }
            Debug.LogWarning($"ChatUIView: {error}");
            _sendButton.interactable = true;
        }

        public void ShowError(string message)
        {
            if (_errorText != null)
            {
                _errorText.text = message;
            }
        }
    }
}
