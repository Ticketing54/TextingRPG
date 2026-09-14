using System;
using System.Collections;
using DG.Tweening;
using TextingRPG.Core;
using TMPro;
using UnityEngine;

namespace TextingRPG.UI
{
    // 위험한 선택의 결과를 화면 중앙에서 보여주는 오버레이.
    // 주사위를 하나씩 굴리고, 정착할 때마다 결과를 아래 슬롯에 하나씩 찍는다.
    // 둘 다 끝나면 등급 라벨을 보여준 뒤 스스로 숨으며 다음 진행(LLM 호출)을 이어간다.
    // ChatController.OnDiceRollRequested를 구독한다. 버튼 등 수동 트리거는 없다.
    public class DiceRollOverlayView : MonoBehaviour
    {
        [SerializeField] CanvasGroup visualGroup;         // alpha/blocksRaycasts로 표시·숨김
        [SerializeField] DiceRollAnimation roller;        // 가운데서 굴러가는 주사위 (1개)
        [SerializeField] TextMeshProUGUI[] landedSlots;   // 굴린 결과가 아래에 하나씩 쌓임
        [SerializeField] TextMeshProUGUI outcomeLabel;
        [SerializeField] float betweenRollsDelay = 0.25f;
        [SerializeField] float afterSettleDelay = 0.7f;

        private ChatController _controller;
        private Action _pendingContinuation;
        private string _pendingLabel;

        public void Bind(ChatController controller)
        {
            if (_controller != null) _controller.OnDiceRollRequested -= HandleRollRequested;
            _controller = controller;
            _controller.OnDiceRollRequested += HandleRollRequested;
        }

        private void Awake() => SetVisible(false);

        private void OnDestroy()
        {
            if (_controller != null) _controller.OnDiceRollRequested -= HandleRollRequested;
        }

        private void HandleRollRequested(DiceRollResult roll, string label, Action continueWith)
        {
            _pendingContinuation = continueWith;
            _pendingLabel = label;

            foreach (var slot in landedSlots) slot.text = "";
            outcomeLabel.text = "";
            SetVisible(true);

            StartCoroutine(RollSequence(new[] { roll.A, roll.B }));
        }

        private IEnumerator RollSequence(int[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int settled = 0;
                bool done = false;
                Action<int> handler = v => { settled = v; done = true; };

                roller.OnRollComplete += handler;
                roller.Roll(values[i]);
                while (!done) yield return null;
                roller.OnRollComplete -= handler;

                if (i < landedSlots.Length)
                {
                    var slot = landedSlots[i];
                    slot.text = roller.FaceGlyph(settled);
                    slot.transform.localScale = Vector3.one;
                    slot.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.8f);
                }

                yield return new WaitForSeconds(betweenRollsDelay);
            }

            outcomeLabel.text = _pendingLabel;
            yield return new WaitForSeconds(afterSettleDelay);

            SetVisible(false);
            var continuation = _pendingContinuation;
            _pendingContinuation = null;
            continuation?.Invoke();
        }

        private void SetVisible(bool visible)
        {
            visualGroup.alpha = visible ? 1f : 0f;
            visualGroup.blocksRaycasts = visible; // 굴리는 동안 뒤쪽 입력 차단
        }
    }
}
