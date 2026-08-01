using System;
using DG.Tweening;
using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public ChatBubbleState State { get; private set; } = ChatBubbleState.Idle;
    public event Action OnPlayComplete;

    private Tween _typingTween;
    private string _fullText;

    public void Play(ChatSender sender, string text)
    {
        verticalLayoutGroup.childAlignment = AlignmentFor(sender);
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
