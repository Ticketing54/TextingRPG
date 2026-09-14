using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 키워드 선택 다음, 게임 시작 전에 플레이어 이름을 받는 패널.
    // 이름이 비어 있으면 확인 버튼이 비활성이다.
    public class PlayerNamePanel : MonoBehaviour
    {
        [SerializeField] TMP_InputField nameInput;
        [SerializeField] Button confirmButton;

        public event Action<string> OnNameConfirmed;

        private void Awake()
        {
            // NameInput은 채팅 입력창 복제라 씬에 interactable=false로 저장돼 있다. 켜준다.
            nameInput.interactable = true;
            confirmButton.interactable = false;
            nameInput.onValueChanged.AddListener(HandleValueChanged);
            confirmButton.onClick.AddListener(Confirm);
        }

        private void OnEnable()
        {
            nameInput.ActivateInputField(); // 패널이 뜨면 바로 입력 가능하도록 포커스
        }

        private void HandleValueChanged(string value)
        {
            confirmButton.interactable = !string.IsNullOrWhiteSpace(value);
        }

        private void Confirm()
        {
            var name = nameInput.text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            OnNameConfirmed?.Invoke(name);
            gameObject.SetActive(false);
        }
    }
}
