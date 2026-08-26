using System;
using System.Collections.Generic;
using TextingRPG.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class ChatBubbleListView : MonoBehaviour
    {
        [SerializeField] ChatBubble chatBubblePrefab;
        [SerializeField] RectTransform content;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] float typingCharsPerSecondOverride = 0f;

        public event Action<ChatMessage> OnMessagePlaybackComplete;

        private readonly Queue<ChatMessage> _pending = new();
        private ChatBubble _current;
        private ChatMessage _currentMessage;
        private ChatController _controller;

        public void Bind(ChatController controller)
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
            _controller = controller;
            _controller.OnMessageAdded += Enqueue;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
        }

        private void Enqueue(ChatMessage message)
        {
            _pending.Enqueue(message);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_current != null) return;
            if (_pending.Count == 0) return;

            var message = _pending.Dequeue();
            _currentMessage = message;
            _current = Instantiate(chatBubblePrefab, content);
            if (typingCharsPerSecondOverride > 0f) _current.CharsPerSecond = typingCharsPerSecondOverride;
            _current.OnPlayComplete += HandleCurrentComplete;
            _current.Play(message.Sender, message.Text);

            ScrollToBottom();
        }

        private void HandleCurrentComplete()
        {
            _current.OnPlayComplete -= HandleCurrentComplete;
            _current = null;
            var completedMessage = _currentMessage;
            _currentMessage = null;
            ScrollToBottom();
            TryPlayNext();

            OnMessagePlaybackComplete?.Invoke(completedMessage);
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
