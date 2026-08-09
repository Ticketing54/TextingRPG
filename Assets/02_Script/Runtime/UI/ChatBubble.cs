using System;
using DG.Tweening;
using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public enum ChatBubbleState
    {
        Idle,
        Typing,
        Completed
    }

    public class ChatBubble : MonoBehaviour
    {
        [SerializeField] VerticalLayoutGroup verticalLayoutGroup;
        [SerializeField] RectTransform rectTransform_Bubble;
        [SerializeField] TextMeshProUGUI textMeshProUGUI_text;
        [SerializeField] float charsPerSecond = 40f;
        [SerializeField] float maxBubbleWidth = 800f;

        public ChatBubbleState State { get; private set; } = ChatBubbleState.Idle;
        public event Action OnPlayComplete;

        public float CharsPerSecond
        {
            get => charsPerSecond;
            set => charsPerSecond = value;
        }

        private Tween _typingTween;
        private string _fullText;
        private VerticalLayoutGroup _bubbleLayoutGroup;

        private void Awake()
        {
            _bubbleLayoutGroup = rectTransform_Bubble.GetComponent<VerticalLayoutGroup>();
        }

        public void Play(ChatSender sender, string text)
        {
            verticalLayoutGroup.childAlignment = AlignmentFor(sender);
            ResizeBubbleWidth(text);
            _typingTween?.Kill();

            if (sender == ChatSender.Player)
            {
                textMeshProUGUI_text.text = text;
                SetState(ChatBubbleState.Completed);
                return;
            }

            StartTyping(text);
        }

        public void Skip()
        {
            if (State != ChatBubbleState.Typing) return;
            _typingTween?.Kill();
            textMeshProUGUI_text.text = _fullText;
            SetState(ChatBubbleState.Completed);
        }

        private void ResizeBubbleWidth(string text)
        {
            float naturalWidth = textMeshProUGUI_text.GetPreferredValues(text, 0f, 0f).x;
            float horizontalPadding = _bubbleLayoutGroup.padding.left + _bubbleLayoutGroup.padding.right;
            float width = Mathf.Min(maxBubbleWidth, naturalWidth + horizontalPadding);
            rectTransform_Bubble.sizeDelta = new Vector2(width, rectTransform_Bubble.sizeDelta.y);
        }

        private void StartTyping(string text)
        {
            _fullText = text;
            SetState(ChatBubbleState.Typing);
            textMeshProUGUI_text.text = new string(' ', text.Length);

            int revealed = 0;
            float duration = Mathf.Max(0.01f, text.Length / charsPerSecond);
            _typingTween = DOTween.To(() => revealed, v => revealed = v, text.Length, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => textMeshProUGUI_text.text = text[..revealed] + new string(' ', text.Length - revealed))
                .OnComplete(() => SetState(ChatBubbleState.Completed));
        }

        private void SetState(ChatBubbleState state)
        {
            State = state;
            if (state == ChatBubbleState.Completed) OnPlayComplete?.Invoke();
        }

        private static TextAnchor AlignmentFor(ChatSender sender) => sender switch
        {
            ChatSender.Player => TextAnchor.UpperRight,
            ChatSender.Narration => TextAnchor.UpperCenter,
            _ => TextAnchor.UpperLeft
        };
    }
}
