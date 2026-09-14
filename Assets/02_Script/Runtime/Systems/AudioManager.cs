using TextingRPG.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.Systems
{
    // 배경음악 + UI 효과음(버튼 호버)을 담당하는 전역 싱글턴. DataManager와 같은 패턴 —
    // 씬에 배치된 평범한 MonoBehaviour, DontDestroyOnLoad 없음(이 프로젝트는 씬이 하나뿐이고
    // "나가기"가 씬 리로드라 다른 싱글턴들처럼 리로드마다 새로 초기화되는 게 맞다).
    // bgmClip/hoverSound는 아직 오디오 파일이 없어 비워둔다 — 나중에 인스펙터에서 연결하면 된다.
    // 클립이 비어있으면 조용히 아무 일도 안 한다(에러 없음).
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance;

        [SerializeField] AudioSource bgmSource;
        [SerializeField] AudioSource sfxSource;
        [SerializeField] AudioClip bgmClip;
        [SerializeField] AudioClip hoverSound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RegisterHoverSounds();
        }

        private void Start()
        {
            if (bgmClip == null) return;
            bgmSource.clip = bgmClip;
            bgmSource.Play();
        }

        public void SetBgmVolume(float volume) => bgmSource.volume = volume;

        public void SetSfxVolume(float volume) => sfxSource.volume = volume;

        public void PlayHoverSound()
        {
            if (hoverSound != null) sfxSource.PlayOneShot(hoverSound);
        }

        // 씬의 Button(비활성 포함)을 전부 찾아서 아직 ButtonHoverSound가 없으면 붙인다.
        // 이미 붙어있는 버튼은 건너뛰므로 여러 번 불러도 안전하다 — KeywordIntroPanel/HistoryPanel처럼
        // 코드로 나중에 버튼을 만드는 곳에서 생성 직후 다시 불러주면 그 버튼들도 잡힌다.
        public void RegisterHoverSounds()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var button in buttons)
            {
                if (button.GetComponent<ButtonHoverSound>() == null)
                    button.gameObject.AddComponent<ButtonHoverSound>();
            }
        }
    }
}
