# 플레이 화면 원형 메뉴 버튼(나가기/설정) 설계

## 배경

메인 메뉴에서 "새 게임"이나 "히스토리"로 들어가면(=`chatUIRoot` 활성화) 메인 메뉴로 돌아갈 방법이
없다. 진행 중 대화든 히스토리 읽기 전용 재생이든 둘 다 막다른 골목이다.

아이폰 AssistiveTouch 스타일의 드래그 가능한 원형 버튼을 만들어 이 문제를 해결한다: 화면
좌/우로 드래그해서 옮길 수 있고, 탭하면 옆으로 메뉴 항목(나가기/설정)이 펼쳐진다.

## 방식

새 컴포넌트 `FloatingMenuButton` 하나로 드래그·스냅·펼침/접힘·나가기를 전부 처리한다.
`ChatBootstrap`이나 다른 기존 스크립트는 건드리지 않는다 — `chatUIRoot`의 자식으로 배치해서
채팅/히스토리 화면이 켜지고 꺼질 때 자동으로 같이 뜨고 사라지게 한다.

### 나가기는 씬 리로드로 처리한다

"나가기"를 누르면 `SceneManager.LoadScene(현재 씬)`으로 씬을 통째로 다시 로드한다. 확인 팝업은
없다 — 진행 중 대화도 매 턴 `SaveSystem.SaveCurrent`로 이미 저장돼 있어 데이터 손실이 없다.

패널 토글(`chatUIRoot.SetActive(false)` + `mainMenuPanel.SetActive(true)`) 방식 대신 씬 리로드를
택한 이유: 지금까지는 "새 게임 → 끝까지 진행"이 한 방향으로만 흘러서 드러나지 않았던 기존 버그가
여럿 있다.

- `ChatBubbleListView.Lock()`이 한 번 걸리면 풀리지 않는다(`_locked`는 되돌리는 API가 없음).
  엔딩 도달 시, 히스토리 재생 시 둘 다 걸린다. 나가기 → 새 게임을 하면 입력이 영원히 잠긴 채로
  시작하게 된다.
- `ChatInputView.Bind()`가 재호출을 고려하지 않고 이벤트 구독(`OnInputStateChanged`)과 버튼
  리스너(`sendButton.onClick`, `inputField.onSubmit`)를 매번 추가만 한다. 같은 실행에서 새 게임을
  두 번째로 시작하면 메시지가 중복 전송된다.
- `DataManager`가 씬 안의 평범한 싱글턴이라(`DontDestroyOnLoad` 없음) 이전 판의
  `storyOutline`/`memory`/`conversation`이 지워지지 않고 새 판에 남을 수 있다.

이 프로젝트는 빌드 설정에 씬이 `PlayScene` 하나뿐이라, 씬 리로드로 위 문제를 개별로 고치지 않고
전부 우회할 수 있다. 리로드 순간의 짧은 프레임 끊김은 감수한다(체감상 거의 없음).

## 추가

`Assets/02_Script/Runtime/UI/FloatingMenuButton.cs` — 아래 동작을 전담하는 단일 MonoBehaviour.

**직렬화 필드**: `mainButton`(RectTransform+Button), `exitButton`(Button), `settingsButton`(Button),
`blocker`(펼쳤을 때만 활성화되는 전체화면 투명 레이캐스트 차단 Image), 스냅 여백, 펼침 이동 거리,
애니메이션 지속시간 등.

**드래그 & 스냅**: `mainButton`이 `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`를 구현.
드래그 중 포인터를 따라 `anchoredPosition` 갱신. `OnEndDrag`에서 화면 중앙 기준 좌/우 중 가까운
쪽 가장자리로 DOTween 트윈 이동(세로 위치는 놓은 자리 유지). 앱을 다시 실행하면 위치를 기억하지
않고 기본 위치(오른쪽 가운데)에서 시작 — 저장 로직 없음.

**탭/드래그 구분**: 별도 임계값 로직 없이 Unity EventSystem 기본 동작에 맡긴다 — 실제로 드래그가
발생하면(`eventData.dragging == true`) `IPointerClickHandler`가 호출되지 않으므로, `mainButton`에
`IPointerClickHandler`만 추가하면 "탭"과 "드래그"가 자연히 구분된다.

**펼침**: 접힌 상태에서 `mainButton` 탭 → `blocker` 활성화 + `exitButton`/`settingsButton`이
`mainButton`의 현재 화면 위치가 왼쪽 절반이면 오른쪽으로, 오른쪽 절반이면 왼쪽으로(항상 화면
안쪽 방향) DOTween으로 약간의 시간차(스태거)를 두고 스케일 0→1·알파 0→1로 나타난다.

**접힘**: 아래 세 경우 전부 같은 접힘 애니메이션(펼침의 역순)으로 처리한다.
- `mainButton`을 다시 탭
- `blocker`(펼친 영역 바깥)를 탭
- 펼쳐지지 않은 상태에서는 애초에 접을 게 없으므로 해당 없음

**나가기**: `exitButton` 클릭 → 접힘 애니메이션 없이 즉시
`SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.

**설정**: `settingsButton.interactable = false` — 눌러도 반응 없음, 자리만 마련
(기존 `MainMenuPanel`의 이어하기/설정 버튼과 동일한 자리표시자 패턴).

## 씬

`Assets/04_Prefab/ChatUI.prefab`의 `ChatUIRoot` 밑에 마지막 자식(형제 중 맨 뒤 = 항상 제일 위에
렌더링, 말풍선/입력창보다 위)으로 추가:

```
FloatingMenu (전체 화면 RectTransform, 앵커 0,0~1,1)
├─ Blocker (평소 비활성)
├─ MainButton (아이콘만 있는 원형 버튼)
├─ ExitButton (아이콘만, 평소 MainButton 위치에 겹쳐 숨어 있음)
└─ SettingsButton (아이콘만, interactable=false)
```

스타일은 기존 다크 패널 스타일과 통일된 원형 아이콘 버튼으로 하되, 정확한 아이콘 모양·색상 등
디테일은 구현 단계에서 정한다.

`chatUIRoot`의 자식이므로 채팅/히스토리 화면이 켜지고 꺼질 때 자동으로 같이 뜨고 사라진다 —
`ChatBootstrap`에 새 필드나 배선을 추가하지 않는다.

## 범위 밖 (later)

- "이어하기" 연동 — 씬 리로드로 나가면 `current.json`이 남아있어도 자동으로 이어지진 않는다.
  기존 계획대로 별도 작업으로 남겨둔다.
- 여기서 다루지 않는 `ChatBubbleListView.Lock()`/`ChatInputView.Bind()`/`DataManager` 싱글턴의
  근본적인 리셋 로직 — 씬 리로드로 우회했을 뿐, 언젠가 "이어하기"나 씬을 안 거치는 다른 전환이
  생기면 다시 마주칠 문제다.
- 버튼 위치 저장(앱 재시작 시 복원) — 필요해지면 나중에 PlayerPrefs로 추가.
