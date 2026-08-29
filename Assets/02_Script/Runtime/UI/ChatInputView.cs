using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 채팅 입력창/전송 버튼의 활성/비활성 표시를 전담하는 뷰.
    // 입력 가능 여부를 스스로 판단하지 않고, ChatBubbleListView가 결정한
    // ChatInputState에만 반응한다 (판단 주체는 항상 하나).
    public class ChatInputView : MonoBehaviour
    {
        [SerializeField] TMP_InputField inputField;
        [SerializeField] Button sendButton;
        [SerializeField] ChatBubbleListView chatBubbleListView;

        private ChatController _controller;

        public void Bind(ChatController controller)
        {
            _controller = controller;

            chatBubbleListView.OnInputStateChanged += HandleInputStateChanged;
            sendButton.onClick.AddListener(SendCurrentInput);
            inputField.onSubmit.AddListener(_ => SendCurrentInput());

            SetInteractable(false);
        }

        private void SendCurrentInput()
        {
            var text = inputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            sendButton.interactable = false;
            _controller.SendPlayerMessage(text);
        }

        private void HandleInputStateChanged(ChatInputState state)
        {
            SetInteractable(state == ChatInputState.InputEnabled);
        }

        private void SetInteractable(bool interactable)
        {
            sendButton.interactable = interactable;
            inputField.interactable = interactable;
        }
    }
}
