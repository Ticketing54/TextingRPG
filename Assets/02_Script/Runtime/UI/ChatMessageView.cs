using TextingRPG.Core;
using TMPro;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private RectTransform _bubble;

        public void Bind(ChatMessage message)
        {
            _text.text = message.Text;
            if (_bubble != null)
            {
                _bubble.pivot = message.Sender == ChatSender.Player
                    ? new Vector2(1f, _bubble.pivot.y)
                    : new Vector2(0f, _bubble.pivot.y);
            }
        }
    }
}
