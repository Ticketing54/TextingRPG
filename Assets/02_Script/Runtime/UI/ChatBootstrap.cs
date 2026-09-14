using System;
using DG.Tweening;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.Systems;
using UnityEngine;

namespace TextingRPG.UI
{
    // ChatController <-> ChatBubbleListView 배선을 실제 Gemini API로 연결하는 부트스트랩.
    // API 키는 프로젝트 루트의 secrets.local.json(커밋 안 됨)에서 읽는다 (코드/에셋에 하드코딩 금지).
    // 흐름: 메인 메뉴 → 키워드 선택 → 플레이어 이름 입력 → 스토리 생성. (또는 메인 메뉴 → 히스토리 → 읽기 전용 재생)
    public class ChatBootstrap : MonoBehaviour
    {
        [SerializeField] ChatBubbleListView chatBubbleListView;
        [SerializeField] ChatInputView chatInputView;
        [SerializeField] DiceRollOverlayView diceRollOverlayView;
        [SerializeField] string geminiModel = "gemini-3.5-flash-lite"; // thinking 토큰 없이 응답하고, 3.6보다 무료 한도가 넉넉함
        [SerializeField] MainMenuPanel mainMenuPanel;
        [SerializeField] CanvasGroup mainMenuPanelGroup;
        [SerializeField] KeywordIntroPanel keywordIntroPanel;
        [SerializeField] PlayerNamePanel playerNamePanel;
        [SerializeField] HistoryPanel historyPanel;
        [SerializeField] SettingsPanel settingsPanel;
        [SerializeField] GameObject chatUIRoot;
        [SerializeField] CanvasGroup chatUIRootGroup;

        private ChatController _controller;
        private string _apiKey;
        private string[] _keywords;

        private void Start()
        {
            _apiKey = LocalSecrets.GetGeminiApiKey();
            if (string.IsNullOrEmpty(_apiKey))
            {
                return;
            }

            chatUIRoot.SetActive(false);
            keywordIntroPanel.gameObject.SetActive(false);
            playerNamePanel.gameObject.SetActive(false);
            historyPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(false);
            mainMenuPanelGroup.alpha = 0f;

            mainMenuPanel.OnNewGameClicked += HandleNewGameClicked;
            mainMenuPanel.OnHistoryClicked += HandleHistoryClicked;
            mainMenuPanel.OnSettingsClicked += HandleSettingsClicked;
            keywordIntroPanel.OnKeywordsConfirmed += HandleKeywordsConfirmed;
            playerNamePanel.OnNameConfirmed += HandleNameConfirmed;
            historyPanel.OnBackClicked += HandleHistoryBackClicked;
            historyPanel.OnEntrySelected += HandleHistoryEntrySelected;
            settingsPanel.OnBackClicked += HandleSettingsBackClicked;
            settingsPanel.OnTypingSpeedChanged += chatBubbleListView.SetTypingSpeed;

            FadeIn(mainMenuPanelGroup, () => mainMenuPanel.PlayIntroAnimation());
        }

        private void HandleNewGameClicked()
        {
            mainMenuPanel.gameObject.SetActive(false);
            keywordIntroPanel.gameObject.SetActive(true);
        }

        private void HandleHistoryClicked()
        {
            mainMenuPanel.gameObject.SetActive(false);
            historyPanel.gameObject.SetActive(true);
            historyPanel.Populate();
        }

        private void HandleHistoryBackClicked()
        {
            historyPanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(true);
        }

        // 설정 화면은 뒤 화면을 끄지 않고 그 위를 덮는 오버레이다 — FloatingMenuButton이 게임 중에
        // 여는 경우에도 같은 방식으로 동작해야 해서, 여기서도 "뒤로 = 그냥 닫기"로 맞춘다.
        private void HandleSettingsClicked()
        {
            settingsPanel.gameObject.SetActive(true);
        }

        private void HandleSettingsBackClicked()
        {
            settingsPanel.gameObject.SetActive(false);
        }

        private void HandleHistoryEntrySelected(string id)
        {
            var data = SaveSystem.LoadHistoryEntry(id);
            if (data == null)
            {
                Debug.LogError($"히스토리 항목을 불러오지 못했다: {id}");
                return;
            }

            historyPanel.gameObject.SetActive(false);

            // LoadHistory는 활성 계층을 전제로 한다 (말풍선 Awake, 레이아웃 리빌드, 스크롤).
            // 비활성 상태에서 먼저 부르면 말풍선이 Awake 없이 생성돼 NullReferenceException이 난다.
            chatUIRoot.SetActive(true);
            chatBubbleListView.LoadHistory(data.History);
            chatInputView.DisableForReadOnly();
        }

        private void HandleKeywordsConfirmed(string[] keywords)
        {
            _keywords = keywords;
            playerNamePanel.gameObject.SetActive(true);
        }

        private void HandleNameConfirmed(string playerName)
        {
            var finalWorldDescription = "\n\n[이번 모험의 키워드] " + string.Join(", ", _keywords);

            ILLMProvider provider = new GeminiProvider(_apiKey, geminiModel);

            _controller = new ChatController(provider, finalWorldDescription, playerName);
            _controller.OnError += HandleError;
            _controller.OnConversationEnded += HandleConversationEnded;
            chatBubbleListView.Bind(_controller);
            chatInputView.Bind(_controller);
            diceRollOverlayView.Bind(_controller);

            chatUIRoot.SetActive(true);
            chatUIRootGroup.alpha = 0f;
            FadeIn(chatUIRootGroup);

            _controller.BeginAdventure();
        }

        private void HandleError(string error)
        {
            Debug.LogError($"Chat error: {error}");
            chatBubbleListView.NotifyError();
        }

        private void HandleConversationEnded()
        {
            Debug.Log("이야기가 종료되었습니다.");
            chatBubbleListView.Lock();
        }

        // 새 화면을 투명에서 불투명으로 서서히 드러낸다. 끝나기 전엔 클릭을 막아서
        // 다 나타나기 전에 버튼이 눌리는 걸 방지한다. onComplete는 페이드가 끝난 뒤 한 번 더 실행할
        // 후속 동작(예: 메인 메뉴 인트로 애니메이션 시작)을 위한 선택적 콜백이다.
        private void FadeIn(CanvasGroup group, Action onComplete = null, float duration = 0.4f)
        {
            group.blocksRaycasts = false;
            DOTween.To(() => group.alpha, v => group.alpha = v, 1f, duration)
                .OnComplete(() =>
                {
                    group.blocksRaycasts = true;
                    onComplete?.Invoke();
                });
        }
    }
}
