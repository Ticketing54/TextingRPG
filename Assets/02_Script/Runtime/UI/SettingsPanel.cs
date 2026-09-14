using System;
using TextingRPG.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 설정 화면. 배경음악/효과음 슬라이더는 아직 실제 재생 시스템이 없어서 값만 들고 있다
    // (재생 시스템이 생기면 그때 연결). 타이핑 속도는 OnTypingSpeedChanged로 실시간 반영된다 —
    // ChatBootstrap이 chatBubbleListView.SetTypingSpeed에 그대로 연결한다.
    //
    // 열려있는 동안은 Time.timeScale을 0으로 멈춘다. UI 입력(버튼/슬라이더)은 Time.timeScale과
    // 무관하게 정상 동작하지만, DOTween 트윈(타이핑/페이드/버튼 등장 등 SetUpdate(true) 없이 쓴 것들)은
    // 같이 멈췄다가 설정을 닫으면 이어서 재생된다.
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] Button backButton;
        [SerializeField] Slider bgmVolumeSlider;
        [SerializeField] Slider sfxVolumeSlider;
        [SerializeField] Slider typingSpeedSlider;

        public event Action OnBackClicked;
        public event Action<float> OnTypingSpeedChanged;

        private void Awake()
        {
            backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
            typingSpeedSlider.onValueChanged.AddListener(value => OnTypingSpeedChanged?.Invoke(value));
            bgmVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetBgmVolume(value));
            sfxVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetSfxVolume(value));
        }

        private void OnEnable() => Time.timeScale = 0f;

        // SetActive(false)뿐 아니라 씬 리로드로 이 오브젝트가 파괴될 때도 먼저 호출되므로,
        // "나가기"로 씬을 리로드해도 timeScale이 0으로 남는 일은 없다.
        private void OnDisable() => Time.timeScale = 1f;
    }
}
