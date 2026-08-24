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
        [SerializeField] TextMeshProUGUI textMeshProUGUI_senderName;
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

        public void Play(ChatSender sender, string text, string senderName = null)
        {
            verticalLayoutGroup.childAlignment = AlignmentFor(sender);
            ResizeBubbleWidth(text);
            ApplySenderName(sender, senderName);
            _typingTween?.Kill();

            if (sender == ChatSender.Player)
            {
                textMeshProUGUI_text.text = text;
                textMeshProUGUI_text.maxVisibleCharacters = text.Length;
                SetState(ChatBubbleState.Completed);
                return;
            }

            StartTyping(text);
        }

        public void Skip()
        {
            if (State != ChatBubbleState.Typing) return;
            _typingTween?.Kill();
            textMeshProUGUI_text.maxVisibleCharacters = _fullText.Length;
            SetState(ChatBubbleState.Completed);
        }

        private const float WrapBoundaryBuffer = 4f; // lineExtents는 줄바꿈 경계값 자체라, 그대로 쓰면 문자별 우측 여백 차이로 재줄바꿈될 수 있음
        private const int MaxWidthCorrectionAttempts = 10;

        private void ResizeBubbleWidth(string text)
        {
            float horizontalPadding = _bubbleLayoutGroup.padding.left + _bubbleLayoutGroup.padding.right;

            var textRect = textMeshProUGUI_text.rectTransform;
            textRect.sizeDelta = new Vector2(maxBubbleWidth - horizontalPadding, textRect.sizeDelta.y);
            textMeshProUGUI_text.text = text;
            textMeshProUGUI_text.ForceMeshUpdate();

            int targetLineCount = textMeshProUGUI_text.textInfo.lineCount;
            float width = Mathf.Min(maxBubbleWidth, WidestLineWidth(textMeshProUGUI_text.textInfo) + horizontalPadding + WrapBoundaryBuffer);

            // 버퍼로도 부족한 경계 케이스(문장부호 등)를 대비한 안전장치: 줄 수가 늘었으면 조금씩 넓혀 재검증
            for (int attempt = 0; attempt < MaxWidthCorrectionAttempts && width < maxBubbleWidth; attempt++)
            {
                textRect.sizeDelta = new Vector2(width - horizontalPadding, textRect.sizeDelta.y);
                textMeshProUGUI_text.ForceMeshUpdate();
                if (textMeshProUGUI_text.textInfo.lineCount <= targetLineCount) break;
                width = Mathf.Min(maxBubbleWidth, width + WrapBoundaryBuffer);
            }

            rectTransform_Bubble.sizeDelta = new Vector2(width, rectTransform_Bubble.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform_Bubble);
        }

        private static float WidestLineWidth(TMP_TextInfo textInfo)
        {
            float widest = 0f;
            for (int i = 0; i < textInfo.lineCount; i++)
            {
                var extents = textInfo.lineInfo[i].lineExtents;
                widest = Mathf.Max(widest, extents.max.x - extents.min.x);
            }
            return widest;
        }

        private void StartTyping(string text)
        {
            _fullText = text;
            SetState(ChatBubbleState.Typing);
            textMeshProUGUI_text.text = text;
            textMeshProUGUI_text.maxVisibleCharacters = 0;

            int revealed = 0;
            float duration = Mathf.Max(0.01f, text.Length / charsPerSecond);
            _typingTween = DOTween.To(() => revealed, v => revealed = v, text.Length, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => textMeshProUGUI_text.maxVisibleCharacters = revealed)
                .OnComplete(() => SetState(ChatBubbleState.Completed));
        }

        private void SetState(ChatBubbleState state)
        {
            State = state;
            if (state == ChatBubbleState.Completed) OnPlayComplete?.Invoke();
        }

        private void ApplySenderName(ChatSender sender, string senderName)
        {
            bool show = sender == ChatSender.Npc && !string.IsNullOrEmpty(senderName);
            textMeshProUGUI_senderName.gameObject.SetActive(show);
            if (show) textMeshProUGUI_senderName.text = senderName;
        }

        private static TextAnchor AlignmentFor(ChatSender sender) => sender switch
        {
            ChatSender.Player => TextAnchor.UpperRight,
            ChatSender.Narration => TextAnchor.UpperCenter,
            _ => TextAnchor.UpperLeft
        };
    }
}
