using System;
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

        // 매 프레임 이 값을 다시 읽으면서 진행하므로(Update 참고), 타이핑 도중에 값을 바꾸면
        // 그 다음 프레임부터 바로 반영된다 — 설정 화면의 타이핑 속도 슬라이더가 이걸 이용한다.
        public float CharsPerSecond
        {
            get => charsPerSecond;
            set => charsPerSecond = value;
        }

        private string _fullText;
        private float _revealedChars;
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
            _revealedChars = 0f;
            SetState(ChatBubbleState.Typing);
            textMeshProUGUI_text.text = text;
            textMeshProUGUI_text.maxVisibleCharacters = 0;
        }

        private void Update()
        {
            if (State != ChatBubbleState.Typing) return;

            float rate = Mathf.Max(0.01f, charsPerSecond); // 0/음수로 인해 영원히 안 끝나는 걸 방지
            _revealedChars += rate * Time.deltaTime;

            if (_revealedChars >= _fullText.Length)
            {
                textMeshProUGUI_text.maxVisibleCharacters = _fullText.Length;
                SetState(ChatBubbleState.Completed);
            }
            else
            {
                textMeshProUGUI_text.maxVisibleCharacters = Mathf.FloorToInt(_revealedChars);
            }
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
