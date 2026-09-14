using System;
using System.Collections.Generic;
using TextingRPG.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public enum ChatInputState
    {
        InputEnabled,
        Waiting,
        Locked
    }

    // 채팅 입력이 지금 가능한지(ChatInputState)를 판단하는 유일한 주체.
    // 말풍선 재생 상태, 전송 쿨다운, 전송 실패(NotifyError), 대화 종료(Lock) 신호를
    // 전부 이 클래스가 모아서 하나의 상태로 판단한다.
    public class ChatBubbleListView : MonoBehaviour
    {
        [SerializeField] ChatBubble chatBubblePrefab;
        [SerializeField] ChatBubble narrationBubblePrefab;
        [SerializeField] RectTransform content;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] float typingCharsPerSecondOverride = 0f;
        [SerializeField] float minSecondsBetweenSends = 4f; // Gemini 무료 티어 RPM(분당 15회) 한도에 맞춘 최소 전송 간격

        public event Action<ChatMessage> OnMessagePlaybackComplete;
        public event Action<ChatInputState> OnInputStateChanged;

        private readonly Queue<ChatMessage> _pending = new();
        private ChatBubble _current;
        private ChatMessage _currentMessage;
        private ChatController _controller;
        private ChatInputState _inputState = ChatInputState.Waiting;
        private bool _locked;
        private float _lastSendTime = float.NegativeInfinity;

        public void Bind(ChatController controller)
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
            _controller = controller;
            _controller.OnMessageAdded += Enqueue;
        }

        // 설정 화면의 타이핑 속도 슬라이더가 실시간으로 부른다. 이후 재생될 말풍선뿐 아니라
        // 지금 타이핑 중인 말풍선에도 즉시 반영된다(ChatBubble이 매 프레임 속도를 다시 읽으므로).
        public void SetTypingSpeed(float charsPerSecond)
        {
            typingCharsPerSecondOverride = charsPerSecond;
            if (_current != null) _current.CharsPerSecond = charsPerSecond;
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnMessageAdded -= Enqueue;
        }

        private void Update()
        {
            // 말풍선이 돌고 있지 않은데 쿨다운 때문에 대기 중이라면, 시간이 지나 풀렸는지 매 프레임 확인한다.
            if (_locked) return;
            if (_current != null || _pending.Count > 0) return;
            RecomputeInputState();
        }

        // 이야기가 완전히 끝났을 때 호출한다. 이후로는 어떤 말풍선 이벤트가 와도 다시 열리지 않는다.
        public void Lock()
        {
            _locked = true;
            SetInputState(ChatInputState.Locked, force: true);
        }

        // 전송이 실패했을 때 호출한다. SendCurrentInput이 낙관적으로 꺼둔 버튼을,
        // 지금 큐/쿨다운 상태를 기준으로 다시 계산해서 되돌려준다.
        public void NotifyError()
        {
            if (_locked) return;
            RecomputeInputState(force: true);
        }

        // 저장된 대화를 읽기 전용으로 재생한다 (히스토리 보기). ChatController 없이 호출한다.
        // 큐/타이핑 없이 즉시 전부 표시하고, 끝나면 Lock()으로 입력을 잠근다.
        public void LoadHistory(List<ChatMessage> messages)
        {
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            _pending.Clear();
            _current = null;
            _currentMessage = null;

            foreach (var message in messages)
            {
                var prefab = message.Sender == ChatSender.Narration ? narrationBubblePrefab : chatBubblePrefab;
                var bubble = Instantiate(prefab, content);
                var displayText = string.IsNullOrEmpty(message.DisplayText) ? message.Text : message.DisplayText;
                bubble.Play(message.Sender, displayText, message.SenderName);
                bubble.Skip();
            }

            ScrollToBottom();
            Lock();
        }

        private void Enqueue(ChatMessage message)
        {
            if (message.Sender == ChatSender.Player) _lastSendTime = Time.time;

            _pending.Enqueue(message);
            TryPlayNext();
        }

        private void TryPlayNext()
        {
            if (_current != null) return;

            if (_pending.Count == 0)
            {
                RecomputeInputState();
                return;
            }

            var message = _pending.Dequeue();
            _currentMessage = message;
            var prefab = message.Sender == ChatSender.Narration ? narrationBubblePrefab : chatBubblePrefab;
            _current = Instantiate(prefab, content);
            if (typingCharsPerSecondOverride > 0f) _current.CharsPerSecond = typingCharsPerSecondOverride;
            _current.OnPlayComplete += HandleCurrentComplete;

            // 플레이어 말풍선은 즉시 완료되므로 대기 상태로 들어가지 않는다.
            if (message.Sender != ChatSender.Player) SetInputState(ChatInputState.Waiting);

            var displayText = string.IsNullOrEmpty(message.DisplayText) ? message.Text : message.DisplayText;
            _current.Play(message.Sender, displayText, message.SenderName);

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

        private void RecomputeInputState(bool force = false)
        {
            if (_current != null || _pending.Count > 0)
            {
                SetInputState(ChatInputState.Waiting, force);
                return;
            }

            float remaining = minSecondsBetweenSends - (Time.time - _lastSendTime);
            SetInputState(remaining <= 0f ? ChatInputState.InputEnabled : ChatInputState.Waiting, force);
        }

        private void SetInputState(ChatInputState state, bool force = false)
        {
            if (_locked && state != ChatInputState.Locked) return;
            if (!force && _inputState == state) return;
            _inputState = state;
            OnInputStateChanged?.Invoke(state);
        }

        private void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
