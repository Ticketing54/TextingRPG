using System;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 부팅 시 첫 화면. 새 게임만 동작하고, 세이브 시스템이 붙기 전까지
    // 이어하기/히스토리/설정은 비활성 자리표시자로 둔다.
    public class MainMenuPanel : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button historyButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;

        public event Action OnNewGameClicked;

        private void Awake()
        {
            newGameButton.onClick.AddListener(() => OnNewGameClicked?.Invoke());
            quitButton.onClick.AddListener(Quit);

            // 세이브 시스템 붙기 전까지 비활성.
            continueButton.interactable = false;
            historyButton.interactable = false;
            settingsButton.interactable = false;
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
