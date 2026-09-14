# 화면 전환 페이드 효과 설계

## 배경

지금은 화면 전환이 전부 `SetActive(true/false)`로 즉시 끊겨서 나타난다. 아래 두 지점에 페이드인을
넣는다:

1. 앱 부팅 → 메인 메뉴가 처음 보일 때
2. 이름 확정 → 채팅창(진행 중 대화)이 보일 때

**채팅화면에서 "나가기"로 메인 메뉴로 돌아가는 경우(`FloatingMenuButton.ExitToMainMenu`)는 별도
구현이 필요 없다** — 이 동작은 씬 전체를 `SceneManager.LoadScene`으로 리로드하는데, 그러면
`ChatBootstrap.Start()`가 처음부터 다시 실행되면서 1번 로직이 자동으로 다시 걸린다.

## 방식

이전 화면은 기존처럼 `SetActive(false)`로 즉시 사라지고, **새 화면만** 투명(alpha 0) →
불투명(alpha 1)으로 페이드인한다(크로스페이드 아님 — 이전 화면에 CanvasGroup을 추가할 필요 없음).

모든 로직은 이미 패널 노출을 전담하고 있는 `ChatBootstrap.cs`에 둔다. 별도 static 클래스나
컴포넌트로 빼지 않는다(이 프로젝트에서 최근 정한 방향 — MonoBehaviour 배선 코드는 그걸 쓰는
스크립트 안에 둔다).

### 변경

- `MainMenuPanel` GameObject, `chatUIRoot` GameObject에 각각 `CanvasGroup` 컴포넌트를 추가한다
  (씬/프리팹 수정).
- `ChatBootstrap`에 `[SerializeField] CanvasGroup mainMenuPanelGroup;`,
  `[SerializeField] CanvasGroup chatUIRootGroup;` 필드를 추가하고 씬에서 위 CanvasGroup들을
  연결한다.
- `ChatBootstrap`에 private 메서드 `FadeIn(CanvasGroup group, float duration = 0.4f)` 추가.
  `DOTween.To(() => group.alpha, v => group.alpha = v, 1f, duration)`로 페이드한다
  (`RectTransform.DOAnchorPos`류 DOTween UI 모듈 shortcut은 이 프로젝트에 없어서 못 쓴다 —
  `FloatingMenuButton.cs`가 쓰는 것과 같은 `DOTween.To(getter, setter, ...)` 패턴). 페이드가
  끝나기 전엔 `group.blocksRaycasts = false`로 막아뒀다가, 끝나면 `true`로 풀어서 다 나타나기
  전에 버튼이 눌리는 걸 막는다.
- `Start()`: API 키 확인을 통과한 뒤, 기존에 다른 패널들을 꺼두는 줄들 옆에
  `mainMenuPanelGroup.alpha = 0;`을 추가하고, 기존 배선이 끝난 마지막에
  `FadeIn(mainMenuPanelGroup);`을 호출한다.
- `HandleNameConfirmed()`: `chatUIRoot.SetActive(true);` 직후 `chatUIRootGroup.alpha = 0;` →
  `FadeIn(chatUIRootGroup);`을 추가한다. 그다음 `_controller.BeginAdventure();`는 순서 그대로 둔다
  (페이드가 끝나길 기다리지 않고 바로 API 호출을 시작해도 무방 — 응답이 오기 전에 페이드가
  끝난다).

### 지속시간

0.4초 고정값(`FadeIn`의 기본 인자). 나중에 느리거나 빠르게 느껴지면 그 숫자만 바꾸면 된다.

## 테스트

`ChatBootstrap`은 MonoBehaviour이자 DOTween 애니메이션 위주라 EditMode 자동 테스트 대상이
아니다(이 프로젝트의 기존 UI 스크립트들과 같은 이유). 컴파일 확인 + 수동 Play 모드 검증으로
마무리한다:

- 앱을 처음 Play하면 메인 메뉴가 즉시 나타나지 않고 0.4초에 걸쳐 서서히 보이는지, 그 사이엔
  버튼이 안 눌리는지
- 새 게임 → 키워드 → 이름 확정 시 채팅창이 즉시 나타나지 않고 서서히 보이는지
- 채팅 화면에서 원형 메뉴의 "나가기"를 누르면 씬이 리로드되면서 메인 메뉴가 다시 페이드인하는지

## 범위 밖 (later)

- 크로스페이드(이전 화면도 같이 페이드아웃) — 지금은 새 화면만 페이드인하기로 함
- 그 외 전환(메인메뉴→키워드, 키워드→이름입력, 메인메뉴→히스토리 등)에는 페이드를 넣지 않음 —
  이번엔 요청받은 두 지점만
