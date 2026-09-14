using TextingRPG.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TextingRPG.UI
{
    // AudioManager.RegisterHoverSounds()가 씬의 모든 Button에 자동으로 붙인다 — 직접 붙일 필요 없다.
    public class ButtonHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => AudioManager.Instance?.PlayHoverSound();
    }
}
