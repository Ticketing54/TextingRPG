# 메인 메뉴 프리팹화 + 타이틀 타이핑·버튼 등장 애니메이션 설계

## 배경

메인 메뉴는 버튼 클릭 방식 그대로 유지하기로 했다(대화식으로 바꾸는 건 보류). 대신 채팅 화면과
겉모습만이라도 비슷한 느낌을 주기 위해, 메인 메뉴가 처음 뜰 때 타이틀 텍스트가 채팅 말풍선처럼
타이핑되고 그 아래 버튼들이 순서대로 나타나는 인트로 연출을 추가한다. 겸사겸사 `MainMenuPanel`을
`FloatingMenu`처럼 독립 프리팹으로 뽑아서 씬 안에서 직접 수정하기 어려웠던 문제도 해결한다.

## 방식

### 1. 프리팹화

`MainMenuPanel`(현재 `PlayScene.unity`에 직접 들어있는 일반 씬 오브젝트, 어떤 프리팹의 자식도
아님)을 `Assets/04_Prefab/MainMenuPanel.prefab`로 뽑아낸다. `FloatingMenu.prefab`을 만들 때 쓴
`PrefabUtility.SaveAsPrefabAssetAndConnect`와 동일한 방식 — 씬에는 그 프리팹을 참조하는 인스턴스만
남고, 프리팹 파일을 더블클릭해서 독립적으로 열어 수정할 수 있게 된다.

### 2. 타이틀 타이핑

`MainMenuPanel.cs`에 `[SerializeField] TMP_Text titleLabel;` 필드를 추가한다. `Awake()`에서
`titleLabel.text`에 이미 들어있는 원래 문구를 `_fullTitle`로 기억해두고, `titleLabel.text = "";`
`titleLabel.maxVisibleCharacters = 0;`로 비워둔다.

재생 방식은 `ChatBubble.cs`의 타이핑 애니메이션과 동일한 패턴을 그대로 가져온다 — `revealed`라는
int 지역변수를 0에서 전체 글자 수까지 `DOTween.To`로 선형 증가시키고, `OnUpdate`에서
`titleLabel.maxVisibleCharacters = revealed`로 반영한다. 속도는 `titleCharsPerSecond`(기본값
30자/초) 직렬화 필드로 조절 가능하게 둔다.

### 3. 버튼 등장

버튼 5개(`NewGameButton`/`ContinueButton`/`HistoryButton`/`SettingsButton`/`QuitButton`) 각각에
`CanvasGroup`을 추가한다. `Awake()`에서 전부 `alpha = 0f`, `blocksRaycasts = false`로 숨겨둔다.

타이틀 타이핑이 끝나면(`OnComplete`) 새게임 → 이어하기 → 히스토리 → 설정 → 종료 순서로, 버튼마다
약간의 시간차(`buttonRevealStagger`, 기본값 0.15초)를 두고 `DOTween.To`로 alpha 0→1 페이드인한다.
각 버튼은 자기 페이드가 끝나는 순간 `blocksRaycasts = true`로 풀려서, 완전히 안 보이는 버튼이
눌리는 일이 없게 한다. (`이어하기`/`설정`은 기존처럼 `interactable = false`라 페이드가 끝나도
여전히 눌리지 않는다 — 자리표시자 상태는 그대로 유지.)

### 4. 재생 시점 — 앱을 처음 켰을 때 한 번만

`ChatBootstrap`이 이미 메인 메뉴 패널 전체를 `CanvasGroup`으로 페이드인시키는 로직
(`FadeIn(mainMenuPanelGroup)`)을 갖고 있다. 이 페이드인이 끝난 직후(`OnComplete`) 이어서
`mainMenuPanel.PlayIntroAnimation()`(타이틀 타이핑 시작)을 호출하도록 연결한다 — 패널이 통째로
서서히 나타난 다음, 그 안에서 타이틀이 타이핑되고 버튼이 순서대로 뜨는 흐름으로 이어진다.

이 호출은 `ChatBootstrap.Start()`에서 **딱 한 번만** 일어난다. 히스토리 화면에서 뒤로가기로
메인 메뉴에 돌아올 때(`HandleHistoryBackClicked`)는 지금처럼 `mainMenuPanel.gameObject
.SetActive(true)`만 호출하고 `PlayIntroAnimation()`은 다시 부르지 않는다 — 첫 재생이 끝난 뒤엔
타이틀 텍스트와 버튼 alpha가 이미 "다 보이는" 상태로 남아있으므로, 별도 처리 없이도 재방문 시
그 상태 그대로 즉시 나타난다.

### `FadeIn` 헬퍼 변경

기존 `ChatBootstrap.FadeIn(CanvasGroup group, float duration = 0.4f)`에 완료 콜백을 추가한다:
`FadeIn(CanvasGroup group, System.Action onComplete = null, float duration = 0.4f)`. 기존
`FadeIn(chatUIRootGroup)` 호출부는 콜백 없이 그대로 두고, `FadeIn(mainMenuPanelGroup)` 호출부만
`FadeIn(mainMenuPanelGroup, () => mainMenuPanel.PlayIntroAnimation())`으로 바꾼다.

## 테스트

`MainMenuPanel`은 MonoBehaviour + DOTween 애니메이션 위주라 이 프로젝트의 다른 UI 스크립트들과
같은 이유로 EditMode 자동 테스트 대상이 아니다. 컴파일 확인 + 수동 Play 모드 검증으로 마무리한다:

- Play 시작 시 메인 메뉴 패널이 페이드인된 뒤, 타이틀이 즉시 다 보이지 않고 글자가 하나씩
  타이핑되는지
- 타이핑이 끝난 뒤 버튼 5개가 새게임→이어하기→히스토리→설정→종료 순서로 시간차를 두고 나타나는지
- 버튼이 다 나타나기 전엔 눌러도 반응이 없는지
- 히스토리 화면에서 뒤로가기로 메인 메뉴에 돌아오면, 타이핑/버튼 등장 애니메이션 없이 즉시 이미
  다 보이는 상태로 나타나는지

## 범위 밖 (later)

- 메인 메뉴를 대화식(나레이션+선택지)으로 바꾸는 것 — 이번엔 보류, 겉모습(타이핑 연출)만 반영
- 타이틀/버튼 외 다른 요소(배경 장식 등)의 추가 연출
