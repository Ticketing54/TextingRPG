using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 부팅 시 첫 화면. 새 게임만 동작하고, 세이브 시스템이 붙기 전까지
    // 이어하기/히스토리/설정은 비활성 자리표시자로 둔다.
    // 앱을 처음 켰을 때 한 번, ChatBootstrap이 PlayIntroAnimation()을 불러서
    // 타이틀이 타이핑되고 버튼이 순서대로 나타나는 연출을 재생한다.
    public class MainMenuPanel : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button historyButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] float titleCharsPerSecond = 30f;
        [SerializeField] float buttonRevealDuration = 0.25f;
        [SerializeField] float buttonRevealStagger = 0.15f;

        public event Action OnNewGameClicked;
        public event Action OnHistoryClicked;

        private string _fullTitle;
        private CanvasGroup[] _buttonGroupsInRevealOrder;

        private void Awake()
        {
            newGameButton.onClick.AddListener(() => OnNewGameClicked?.Invoke());
            historyButton.onClick.AddListener(() => OnHistoryClicked?.Invoke());
            quitButton.onClick.AddListener(Quit);

            // 이어하기는 실제 재개 로직이 붙기 전까지, 설정은 조절할 항목이 생기기 전까지 비활성.
            continueButton.interactable = false;
            settingsButton.interactable = false;

            _buttonGroupsInRevealOrder = new[]
            {
                newGameButton.GetComponent<CanvasGroup>(),
                continueButton.GetComponent<CanvasGroup>(),
                historyButton.GetComponent<CanvasGroup>(),
                settingsButton.GetComponent<CanvasGroup>(),
                quitButton.GetComponent<CanvasGroup>(),
            };

            _fullTitle = titleLabel.text;
            titleLabel.text = "";
            titleLabel.maxVisibleCharacters = 0;

            foreach (var group in _buttonGroupsInRevealOrder)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        // 앱을 처음 켰을 때 딱 한 번, ChatBootstrap이 메인 메뉴 패널 페이드인 직후에 호출한다.
        // 타이틀이 다 타이핑되면 버튼들이 순서대로 나타나고, 이후엔 그 상태가 계속 유지된다
        // (히스토리에서 뒤로가기로 돌아와도 다시 재생되지 않음 — 이미 다 보이는 상태 그대로 남아있다).
        public void PlayIntroAnimation()
        {
            int revealed = 0;
            float duration = Mathf.Max(0.01f, _fullTitle.Length / titleCharsPerSecond);
            DOTween.To(() => revealed, v => revealed = v, _fullTitle.Length, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => titleLabel.maxVisibleCharacters = revealed)
                .OnComplete(RevealButtons);
        }

        private void RevealButtons()
        {
            for (int i = 0; i < _buttonGroupsInRevealOrder.Length; i++)
            {
                var group = _buttonGroupsInRevealOrder[i];
                DOTween.To(() => group.alpha, v => group.alpha = v, 1f, buttonRevealDuration)
                    .SetDelay(i * buttonRevealStagger)
                    .OnComplete(() => group.blocksRaycasts = true);
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
