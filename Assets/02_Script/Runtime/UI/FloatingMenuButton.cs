using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 아이폰 AssistiveTouch 스타일 원형 메뉴 버튼. 자기 자신의 RectTransform을 드래그해서 화면 좌/우로
    // 옮길 수 있고(놓으면 가까운 가장자리에 스냅), 탭하면 나가기/설정 버튼이 화면 안쪽 방향으로 펼쳐진다.
    // chatUIRoot의 자식으로 배치해서 채팅/히스토리 화면이 켜지고 꺼질 때 같이 뜨고 사라진다 —
    // ChatBootstrap 등 다른 스크립트는 이 컴포넌트를 몰라도 된다.
    public class FloatingMenuButton : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] Button exitButton;
        [SerializeField] Button settingsButton;
        [SerializeField] GameObject blocker;       // 펼쳤을 때만 활성화되는 전체화면 투명 차단막
        [SerializeField] Button blockerButton;
        [SerializeField] float edgeMargin = 16f;
        [SerializeField] float expandSpacing = 100f;
        [SerializeField] float snapDuration = 0.25f;
        [SerializeField] float expandDuration = 0.2f;
        [SerializeField] float expandStagger = 0.06f;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private Canvas _canvas;
        private RectTransform _exitRect;
        private CanvasGroup _exitGroup;
        private RectTransform _settingsRect;
        private CanvasGroup _settingsGroup;
        private bool _expanded;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _parentRect = (RectTransform)transform.parent;
            _canvas = GetComponentInParent<Canvas>();

            _exitRect = exitButton.GetComponent<RectTransform>();
            _exitGroup = exitButton.GetComponent<CanvasGroup>();
            _settingsRect = settingsButton.GetComponent<RectTransform>();
            _settingsGroup = settingsButton.GetComponent<CanvasGroup>();

            HideSubButtonInstantly(_exitGroup);
            HideSubButtonInstantly(_settingsGroup);
            settingsButton.interactable = false; // 자리표시자 — 아직 기능 없음
            blocker.SetActive(false);

            exitButton.onClick.AddListener(ExitToMainMenu);
            blockerButton.onClick.AddListener(Collapse);
        }

        private static void HideSubButtonInstantly(CanvasGroup group)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        // IBeginDragHandler가 있어야 Unity EventSystem이 이 오브젝트에 드래그를 시작하고
        // OnDrag/OnEndDrag를 계속 보내준다. 실제 처리는 OnDrag/OnEndDrag에서 한다.
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_expanded) return; // 펼쳐진 동안은 드래그로 옮길 수 없다 (AssistiveTouch와 동일)
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            _rect.anchoredPosition += eventData.delta / scale;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_expanded) return;
            float buttonRadius = _rect.rect.width / 2f;
            float targetX = FloatingMenuLayout.SnapTargetX(
                _rect.anchoredPosition.x, _parentRect.rect.width, buttonRadius, edgeMargin);
            _rect.DOKill();
            _rect.DOAnchorPosX(targetX, snapDuration).SetEase(Ease.OutBack);
        }

        // 실제로 드래그가 일어나면(EventSystem이 dragging으로 판단) 이 콜백은 호출되지 않으므로
        // 별도 임계값 로직 없이 탭/드래그가 자연히 구분된다.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_expanded) Collapse();
            else Expand();
        }

        private void Expand()
        {
            _expanded = true;
            blocker.SetActive(true);

            int dir = FloatingMenuLayout.ExpandDirectionSign(_rect.anchoredPosition.x);
            Vector2 basePos = _rect.anchoredPosition;

            _exitRect.DOKill();
            _settingsRect.DOKill();
            _exitGroup.DOKill();
            _settingsGroup.DOKill();

            _exitRect.anchoredPosition = basePos;
            _settingsRect.anchoredPosition = basePos;
            _exitGroup.blocksRaycasts = true;
            _settingsGroup.blocksRaycasts = true;

            _exitRect.DOAnchorPos(basePos + new Vector2(dir * expandSpacing, 0f), expandDuration).SetEase(Ease.OutBack);
            _exitGroup.DOFade(1f, expandDuration);

            _settingsRect.DOAnchorPos(basePos + new Vector2(dir * expandSpacing * 2f, 0f), expandDuration)
                .SetEase(Ease.OutBack).SetDelay(expandStagger);
            _settingsGroup.DOFade(1f, expandDuration).SetDelay(expandStagger);
        }

        private void Collapse()
        {
            _expanded = false;
            Vector2 basePos = _rect.anchoredPosition;

            _exitRect.DOKill();
            _settingsRect.DOKill();
            _exitGroup.DOKill();
            _settingsGroup.DOKill();

            _exitRect.DOAnchorPos(basePos, expandDuration).SetEase(Ease.InBack);
            _exitGroup.DOFade(0f, expandDuration).OnComplete(() => _exitGroup.blocksRaycasts = false);

            _settingsRect.DOAnchorPos(basePos, expandDuration).SetEase(Ease.InBack);
            _settingsGroup.DOFade(0f, expandDuration).OnComplete(() =>
            {
                _settingsGroup.blocksRaycasts = false;
                blocker.SetActive(false);
            });
        }

        private void ExitToMainMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
