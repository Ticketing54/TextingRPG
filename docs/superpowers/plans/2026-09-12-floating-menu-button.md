# 원형 메뉴 버튼(나가기/설정) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 아이폰 AssistiveTouch 스타일의 드래그 가능한 원형 버튼 `FloatingMenuButton`을 만든다. 화면 좌/우로 드래그해서 옮기면 가까운 가장자리에 스냅되고, 탭하면 나가기/설정 아이콘이 화면 안쪽 방향으로 펼쳐진다. "나가기"는 씬을 리로드해서 메인 메뉴로 돌아간다.

**Architecture:** 순수 스냅/펼침-방향 계산은 `FloatingMenuLayout`(정적 클래스, Unity 타입 비의존)로 분리해 EditMode에서 테스트한다. `FloatingMenuButton`(MonoBehaviour)이 드래그/탭 입력을 받아 이 계산 결과를 DOTween 애니메이션에 적용하고, 나가기 클릭 시 `SceneManager.LoadScene`으로 씬을 리로드한다. `ChatUI.prefab`의 `ChatUIRoot` 밑에 새 자식 계층으로 배치해 `ChatBootstrap` 등 기존 스크립트는 전혀 건드리지 않는다.

**Tech Stack:** Unity 6000.3.10f1, C# / Unity Test Framework(NUnit, EditMode), DOTween(이미 프로젝트 의존성), TextMeshPro.

## Global Constraints

- 새 컴포넌트 `FloatingMenuButton` 하나로 드래그·스냅·펼침/접힘·나가기를 전부 처리한다 — `ChatBootstrap`을 비롯한 기존 스크립트는 수정하지 않는다.
- `ChatUI.prefab`의 `ChatUIRoot` 밑, 형제 중 맨 뒤(항상 최상단 렌더링)에 배치한다. `chatUIRoot`의 자식이므로 채팅/히스토리 화면과 자동으로 같이 뜨고 사라진다.
- 메뉴 항목은 "나가기"와 "설정" 2개뿐. 설정은 `interactable = false`인 자리표시자다(기존 `MainMenuPanel`의 이어하기/설정과 동일한 패턴).
- 버튼/하위 항목은 아이콘만 — 텍스트 라벨 없음.
- 펼침 방향은 화면 위치 기준 자동 결정(왼쪽 절반이면 오른쪽으로, 오른쪽 절반이면 왼쪽으로 — 항상 화면 안쪽).
- 드래그를 놓으면 화면 좌/우 가장자리 중 가까운 쪽으로 스냅한다. 세로 위치는 놓은 자리 그대로.
- 버튼 위치는 앱을 다시 실행하면 기억하지 않는다(PlayerPrefs 등 영속화 없음) — 기본 위치(오른쪽 가운데)에서 시작.
- 펼쳐진 상태에서 메인 버튼을 다시 탭하거나, 펼친 영역 바깥(Blocker)을 탭하면 접힌다.
- "나가기" 클릭 시 확인 팝업 없이 즉시 `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`로 씬을 리로드한다.
- 테스트 실행은 Unity MCP의 `run_tests` 도구(`mode: "EditMode"`)를 사용한다. 프리팹/씬 구성은 `execute_code` 도구로 편집기 스크립트를 실행해 처리한다(`ChatBubble.prefab`을 만들 때와 동일한 기존 컨벤션).

---

### Task 1: `FloatingMenuLayout` 순수 계산 로직

**Files:**
- Create: `Assets/02_Script/Runtime/UI/FloatingMenuLayout.cs`
- Test: `Assets/02_Script/Tests/EditMode/FloatingMenuLayoutTests.cs`

**Interfaces:**
- Produces: `FloatingMenuLayout.SnapTargetX(float currentX, float parentWidth, float buttonRadius, float edgeMargin) : float`, `FloatingMenuLayout.ExpandDirectionSign(float currentX) : int` — Task 2(`FloatingMenuButton`)가 이 둘을 소비한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/02_Script/Tests/EditMode/FloatingMenuLayoutTests.cs`를 새로 만든다:

```csharp
using NUnit.Framework;
using TextingRPG.UI;

namespace TextingRPG.Tests
{
    public class FloatingMenuLayoutTests
    {
        [TestCase(-100f, 800f, 40f, 16f, -344f)]
        [TestCase(-1f, 800f, 40f, 16f, -344f)]
        public void SnapTargetX_LeftOfCenter_ReturnsNegativeEdge(float currentX, float parentWidth, float radius, float margin, float expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.SnapTargetX(currentX, parentWidth, radius, margin), 0.001f);
        }

        [TestCase(0f, 800f, 40f, 16f, 344f)]
        [TestCase(250f, 800f, 40f, 16f, 344f)]
        public void SnapTargetX_AtOrRightOfCenter_ReturnsPositiveEdge(float currentX, float parentWidth, float radius, float margin, float expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.SnapTargetX(currentX, parentWidth, radius, margin), 0.001f);
        }

        [TestCase(-50f, 1)]
        [TestCase(-1f, 1)]
        public void ExpandDirectionSign_LeftOfCenter_ReturnsPositiveOne(float currentX, int expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.ExpandDirectionSign(currentX));
        }

        [TestCase(0f, -1)]
        [TestCase(50f, -1)]
        public void ExpandDirectionSign_AtOrRightOfCenter_ReturnsNegativeOne(float currentX, int expected)
        {
            Assert.AreEqual(expected, FloatingMenuLayout.ExpandDirectionSign(currentX));
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → 컴파일 실패 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.FloatingMenuLayoutTests"]`)를 실행한다.
Expected: 컴파일 실패 (`TextingRPG.UI.FloatingMenuLayout`가 아직 없음).

- [ ] **Step 3: `FloatingMenuLayout.cs` 작성**

```csharp
namespace TextingRPG.UI
{
    // 원형 메뉴 버튼의 스냅/펼침 방향을 결정하는 순수 계산. Unity 타입에 의존하지 않아 EditMode에서 바로 테스트한다.
    public static class FloatingMenuLayout
    {
        // 드래그를 놓았을 때 스냅할 x좌표(부모 기준 anchoredPosition.x).
        // 화면 중앙보다 왼쪽이면 왼쪽 가장자리로, 아니면(중앙 포함) 오른쪽 가장자리로.
        public static float SnapTargetX(float currentX, float parentWidth, float buttonRadius, float edgeMargin)
        {
            float edgeX = parentWidth / 2f - buttonRadius - edgeMargin;
            return currentX < 0f ? -edgeX : edgeX;
        }

        // 펼칠 때 하위 버튼이 이동할 방향 부호.
        // 화면 중앙보다 왼쪽이면 +1(오른쪽으로), 아니면(중앙 포함) -1(왼쪽으로) — 항상 화면 안쪽을 향한다.
        public static int ExpandDirectionSign(float currentX) => currentX < 0f ? 1 : -1;
    }
}
```

- [ ] **Step 4: 테스트 실행 → 통과 확인**

`run_tests` (`mode: "EditMode"`, `test_names: ["TextingRPG.Tests.FloatingMenuLayoutTests"]`)를 실행한다.
Expected: PASS (8개 테스트 전부).

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/UI/FloatingMenuLayout.cs Assets/02_Script/Tests/EditMode/FloatingMenuLayoutTests.cs
git commit -m "feat: add FloatingMenuLayout pure snap/expand-direction calculation"
```

---

### Task 2: `FloatingMenuButton` 컴포넌트

**Files:**
- Create: `Assets/02_Script/Runtime/UI/FloatingMenuButton.cs`

**Interfaces:**
- Consumes: `FloatingMenuLayout.SnapTargetX`/`ExpandDirectionSign` (Task 1).
- Produces: `[SerializeField]` 필드 `exitButton`(Button), `settingsButton`(Button), `blocker`(GameObject), `blockerButton`(Button), `edgeMargin`/`expandSpacing`/`snapDuration`/`expandDuration`/`expandStagger`(float) — Task 3에서 씬(정확히는 프리팹) 구성 스크립트가 이 필드들을 채운다. 컴포넌트는 자기 자신의 `RectTransform`(부모 기준 anchoredPosition으로 위치를 다룸)에 부착된다.
- 이 컴포넌트는 드래그/탭 입력과 DOTween 애니메이션 위주라 EditMode 자동 테스트를 두지 않는다(`ChatBubbleListView`/`DiceRollOverlayView`와 같은 이유) — 실제 동작 검증은 프리팹에 배치된 뒤 Task 3의 수동 Play 모드 검증에서 함께 확인한다.

- [ ] **Step 1: `FloatingMenuButton.cs` 작성**

```csharp
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
    //
    // 애니메이션은 RectTransform.DOAnchorPos/CanvasGroup.DOFade 같은 DOTween UI 모듈 shortcut 대신
    // DOTween.To(getter, setter, ...)로 직접 값을 트윈한다 — 이 프로젝트엔 그 UI 모듈이 없다
    // (ChatBubble.cs가 타이핑 애니메이션에 쓰는 것과 동일한 패턴). Kill()도 DOKill() 확장 대신
    // 트윈 참조를 직접 들고 있다가 Kill()하는 방식을 쓴다(역시 ChatBubble.cs와 동일).
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

        private Tween _snapTween;
        private Tween _exitMoveTween;
        private Tween _exitFadeTween;
        private Tween _settingsMoveTween;
        private Tween _settingsFadeTween;

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

            _snapTween?.Kill();
            _snapTween = DOTween.To(
                    () => _rect.anchoredPosition.x,
                    x => _rect.anchoredPosition = new Vector2(x, _rect.anchoredPosition.y),
                    targetX, snapDuration)
                .SetEase(Ease.OutBack);
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

            _exitMoveTween?.Kill();
            _exitFadeTween?.Kill();
            _settingsMoveTween?.Kill();
            _settingsFadeTween?.Kill();

            _exitRect.anchoredPosition = basePos;
            _settingsRect.anchoredPosition = basePos;
            _exitGroup.blocksRaycasts = true;
            _settingsGroup.blocksRaycasts = true;

            var exitTarget = basePos + new Vector2(dir * expandSpacing, 0f);
            _exitMoveTween = DOTween.To(() => _exitRect.anchoredPosition, v => _exitRect.anchoredPosition = v, exitTarget, expandDuration)
                .SetEase(Ease.OutBack);
            _exitFadeTween = DOTween.To(() => _exitGroup.alpha, v => _exitGroup.alpha = v, 1f, expandDuration);

            var settingsTarget = basePos + new Vector2(dir * expandSpacing * 2f, 0f);
            _settingsMoveTween = DOTween.To(() => _settingsRect.anchoredPosition, v => _settingsRect.anchoredPosition = v, settingsTarget, expandDuration)
                .SetEase(Ease.OutBack).SetDelay(expandStagger);
            _settingsFadeTween = DOTween.To(() => _settingsGroup.alpha, v => _settingsGroup.alpha = v, 1f, expandDuration)
                .SetDelay(expandStagger);
        }

        private void Collapse()
        {
            _expanded = false;
            Vector2 basePos = _rect.anchoredPosition;

            _exitMoveTween?.Kill();
            _exitFadeTween?.Kill();
            _settingsMoveTween?.Kill();
            _settingsFadeTween?.Kill();

            _exitMoveTween = DOTween.To(() => _exitRect.anchoredPosition, v => _exitRect.anchoredPosition = v, basePos, expandDuration)
                .SetEase(Ease.InBack);
            _exitFadeTween = DOTween.To(() => _exitGroup.alpha, v => _exitGroup.alpha = v, 0f, expandDuration)
                .OnComplete(() => _exitGroup.blocksRaycasts = false);

            _settingsMoveTween = DOTween.To(() => _settingsRect.anchoredPosition, v => _settingsRect.anchoredPosition = v, basePos, expandDuration)
                .SetEase(Ease.InBack);
            _settingsFadeTween = DOTween.To(() => _settingsGroup.alpha, v => _settingsGroup.alpha = v, 0f, expandDuration)
                .OnComplete(() =>
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
```

> **실행 중 확인된 사실(플랜 작성 시점엔 몰랐음):** 이 프로젝트의 DOTween 설치엔 UI 모듈이 없어
> `RectTransform.DOAnchorPos`/`DOAnchorPosX`/`CanvasGroup.DOFade`/`DOKill()` 확장 메서드가
> 컴파일되지 않는다(`CS1061`/`CS1929`). 위 코드는 이미 그 사실을 반영해 `DOTween.To(getter, setter, ...)` +
> 트윈 참조 직접 `Kill()`로 수정된 최종본이다 — 처음 작성 시 `DOAnchorPos`/`DOFade`로 썼다가
> 컴파일 에러로 발견하고 고쳤다.

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`)로 재컴파일을 요청하고, `read_console`(`types: ["error"]`)로 에러가 없는지 확인한다.
Expected: 에러 0건.

- [ ] **Step 3: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: 기존 테스트 전부 PASS (이 파일은 아무 기존 테스트도 건드리지 않지만, 컴파일이 프로젝트 전체에 깨끗이 반영됐는지 확인).

- [ ] **Step 4: Commit**

```bash
git add Assets/02_Script/Runtime/UI/FloatingMenuButton.cs
git commit -m "feat: add FloatingMenuButton drag/snap/expand/exit component"
```

---

### Task 3: 프리팹 구성 + 수동 End-to-End 검증

**Files:**
- Modify: `Assets/04_Prefab/ChatUI.prefab`

**Interfaces:**
- Consumes: `TextingRPG.UI.FloatingMenuButton` (Task 2)의 직렬화 필드(`exitButton`/`settingsButton`/`blocker`/`blockerButton`).
- 이 태스크로 플랜의 마지막 산출물이 나온다 — 자동화 테스트 대상 아님, 수동 Play 모드 검증으로 마무리. `PlayScene.unity`는 수정하지 않는다(씬의 `ChatUI` 인스턴스가 프리팹 변경을 자동으로 반영한다).

- [ ] **Step 1: 컴파일 확인 (사전 점검)**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후 `read_console`(`types: ["error"]`)로 에러 0건 확인. (Task 2 커밋 이후 도메인 리로드가 끝난 상태인지 재확인하는 절차 — `FloatingMenuButton` 타입이 컴파일된 어셈블리에 존재해야 다음 스텝의 `execute_code`가 그 타입을 참조할 수 있다.)

- [ ] **Step 2: `execute_code`로 `ChatUI.prefab`에 FloatingMenu 계층 구성**

`mcp__UnityMCP__execute_code`(`action: "execute"`)로 아래 코드를 실행한다. `ChatUIRoot`(이미 화면 전체를 덮는 `anchorMin(0,0)`~`anchorMax(1,1)` RectTransform) 밑에 `FloatingMenu` 컨테이너를 만들고, 그 안에 `MainButton`/`ExitButton`/`SettingsButton`/`Blocker`를 배치한 뒤 `FloatingMenuButton` 컴포넌트를 붙이고 필드를 연결한다.

> **실행 중 확인된 사실(플랜 작성 시점엔 몰랐음):** `execute_code`의 컴파일러가 Roslyn 없이 CodeDom(C# 6)으로
> 동작해서, 코드 최상단 `using` 지시문과 로컬 함수(C# 7+)를 못 쓴다 — 전부 완전정규화 이름과
> `System.Func<>` 람다로 바꿨다. 또한 이 in-memory 컴파일 단위는 프로젝트 커스텀 타입을 `using`으로
> 직접 참조하지 못해(`FloatingMenuButton`을 `AddComponent<T>()`로 못 붙임) `AppDomain.CurrentDomain.GetAssemblies()`에서
> 타입을 리플렉션으로 찾아 `AddComponent(Type)`으로 붙였다. 아이콘 글리프도 `…`/`←`/`§`로 바뀌었다 —
> 원래 쓰려던 `⋯`/`⚙`는 TMP 기본 폰트(`LiberationSans SDF`)에 없어 `HasCharacter`로 확인 후 교체했다.

```csharp
System.Type floatingMenuButtonType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var t = asm.GetType("TextingRPG.UI.FloatingMenuButton");
    if (t != null) { floatingMenuButtonType = t; break; }
}
if (floatingMenuButtonType == null) return "FloatingMenuButton 타입을 로드된 어셈블리에서 찾지 못함";

string prefabPath = "Assets/04_Prefab/ChatUI.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
var chatUIRoot = root.transform.Find("ChatUIRoot");
if (chatUIRoot == null) return "ChatUIRoot를 찾지 못함 — 프리팹 구조 확인 필요";

var knob = UnityEditor.AssetDatabase.GetBuiltinExtraResource<UnityEngine.Sprite>("UI/Skin/Knob.psd");
var darkColor = new UnityEngine.Color(0.553f, 0.433f, 0.433f, 1f);

// FloatingMenu — 화면 전체를 덮는 컨테이너. ChatUIRoot의 마지막 자식 = 항상 최상단 렌더링.
var menuGO = new UnityEngine.GameObject("FloatingMenu", typeof(UnityEngine.RectTransform));
menuGO.transform.SetParent(chatUIRoot, false);
var menuRect = menuGO.GetComponent<UnityEngine.RectTransform>();
menuRect.anchorMin = UnityEngine.Vector2.zero;
menuRect.anchorMax = UnityEngine.Vector2.one;
menuRect.offsetMin = UnityEngine.Vector2.zero;
menuRect.offsetMax = UnityEngine.Vector2.zero;
menuGO.transform.SetAsLastSibling();

System.Func<string, string, float, UnityEngine.UI.Button> makeCircleButton = (name, icon, diameter) =>
{
    var go = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
    go.transform.SetParent(menuRect, false);

    var rect = go.GetComponent<UnityEngine.RectTransform>();
    rect.anchorMin = rect.anchorMax = new UnityEngine.Vector2(0.5f, 0.5f);
    rect.pivot = new UnityEngine.Vector2(0.5f, 0.5f);
    rect.sizeDelta = new UnityEngine.Vector2(diameter, diameter);

    var image = go.GetComponent<UnityEngine.UI.Image>();
    image.sprite = knob;
    image.color = darkColor;
    image.preserveAspect = true;

    var button = go.GetComponent<UnityEngine.UI.Button>();
    button.targetGraphic = image;

    var labelGO = new UnityEngine.GameObject("Icon", typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
    labelGO.transform.SetParent(rect, false);
    var labelRect = labelGO.GetComponent<UnityEngine.RectTransform>();
    labelRect.anchorMin = UnityEngine.Vector2.zero;
    labelRect.anchorMax = UnityEngine.Vector2.one;
    labelRect.offsetMin = UnityEngine.Vector2.zero;
    labelRect.offsetMax = UnityEngine.Vector2.zero;
    var tmp = labelGO.GetComponent<TMPro.TextMeshProUGUI>();
    tmp.text = icon;
    tmp.alignment = TMPro.TextAlignmentOptions.Center;
    tmp.fontSize = diameter * 0.45f;
    tmp.color = UnityEngine.Color.white;
    tmp.raycastTarget = false;

    return button;
};

var mainButton = makeCircleButton("MainButton", "…", 80f); // 줄임표 — LiberationSans SDF에서 확인된 글리프
var mainRect = mainButton.GetComponent<UnityEngine.RectTransform>();
// FloatingMenuLayout.SnapTargetX(0, 800, 40, 16)과 같은 값 — 오른쪽 가장자리에서 시작(기본 위치).
mainRect.anchoredPosition = new UnityEngine.Vector2(344f, 0f);

var exitButton = makeCircleButton("ExitButton", "←", 72f);
exitButton.gameObject.AddComponent<UnityEngine.CanvasGroup>();

var settingsButton = makeCircleButton("SettingsButton", "§", 72f); // 섹션 기호 — ⚙는 폰트에 없어 대체
settingsButton.gameObject.AddComponent<UnityEngine.CanvasGroup>();

// Blocker — 펼쳤을 때만 활성화되는 전체화면 투명 차단막. MainButton보다 먼저(=아래) 그려야
// MainButton/ExitButton/SettingsButton이 그 위에서 계속 클릭 가능하다.
var blockerGO = new UnityEngine.GameObject("Blocker", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
blockerGO.transform.SetParent(menuRect, false);
var blockerRect = blockerGO.GetComponent<UnityEngine.RectTransform>();
blockerRect.anchorMin = UnityEngine.Vector2.zero;
blockerRect.anchorMax = UnityEngine.Vector2.one;
blockerRect.offsetMin = UnityEngine.Vector2.zero;
blockerRect.offsetMax = UnityEngine.Vector2.zero;
var blockerImage = blockerGO.GetComponent<UnityEngine.UI.Image>();
blockerImage.color = new UnityEngine.Color(0f, 0f, 0f, 0f);
var blockerButton = blockerGO.GetComponent<UnityEngine.UI.Button>();
blockerButton.targetGraphic = blockerImage;
blockerButton.transition = UnityEngine.UI.Selectable.Transition.None;
blockerGO.transform.SetSiblingIndex(0);

// FloatingMenuButton은 드래그/탭을 직접 받아야 하므로 MainButton 오브젝트에 붙인다.
var menuButtonComp = mainButton.gameObject.AddComponent(floatingMenuButtonType) as UnityEngine.Object;
var so = new UnityEditor.SerializedObject(menuButtonComp);
so.FindProperty("exitButton").objectReferenceValue = exitButton;
so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
so.FindProperty("blocker").objectReferenceValue = blockerGO;
so.FindProperty("blockerButton").objectReferenceValue = blockerButton;
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
UnityEditor.AssetDatabase.Refresh();

return "FloatingMenu built under ChatUIRoot";
```

Expected: 반환값 `"FloatingMenu built under ChatUIRoot"`, 예외 없음.

- [ ] **Step 3: 컴파일/콘솔 확인**

`refresh_unity` (`mode: "force"`, `scope: "all"`, `compile: "request"`) 후 `read_console`(`types: ["error", "warning"]`)로 확인한다.
Expected: 에러 0건. (프리팹에 누락된 컴포넌트 참조가 있으면 여기서 `Missing MonoBehaviour` 류 경고로 드러난다.)

- [ ] **Step 4: 수동 End-to-End 검증 (Play 모드)**

Play 모드로 진입해서 새 게임(또는 히스토리 재생)으로 채팅 화면에 진입한 뒤 아래를 순서대로 확인한다:

1. 화면 오른쪽 가운데에 원형 버튼(`⋯`)이 떠 있는지 확인
2. 버튼을 왼쪽으로 드래그해서 놓으면 화면 왼쪽 가장자리로 스냅되는지, 오른쪽으로 드래그해서 놓으면 오른쪽 가장자리로 스냅되는지 확인
3. 왼쪽에 스냅된 상태에서 탭하면 나가기(`←`)/설정(`⚙`) 아이콘이 오른쪽(화면 안쪽)으로 펼쳐지는지, 오른쪽에 스냅된 상태에서 탭하면 왼쪽으로 펼쳐지는지 확인
4. 펼쳐진 상태에서 메인 버튼을 다시 탭하면 접히는지, 펼쳐진 상태에서 버튼 주변 빈 화면(말풍선 영역 등)을 탭해도 접히는지 확인
5. 설정 아이콘은 눌러도 아무 반응이 없는지(자리표시자) 확인
6. 진행 중인 대화에서 나가기(`←`)를 누르면 확인 팝업 없이 즉시 메인 메뉴로 돌아가는지 확인
7. 새 게임을 다시 시작해서 정상적으로 처음부터 진행되는지 확인 (씬 리로드로 이전 상태가 완전히 초기화됐는지)
8. 히스토리 화면에서 항목을 열람하는 중에도 같은 버튼이 뜨고, 나가기가 똑같이 동작하는지 확인
9. 아이콘 글리프가 네모 박스(□)로 깨져 보이면 TMP 기본 폰트가 해당 문자를 지원하지 않는 것 — 다른 유니코드 문자로 교체 필요(플랜 밖의 후속 조정)

- [ ] **Step 5: Commit**

```bash
git add Assets/04_Prefab/ChatUI.prefab
git commit -m "feat: add draggable floating menu button (exit/settings) to chat UI"
```

---

## Self-Review Notes

- **스펙 커버리지**: 순수 스냅/펼침 방향 계산(Task 1), 드래그·스냅·펼침/접힘·나가기 동작(Task 2), 프리팹 배치 및 필드 연결 + 수동 검증(Task 3) — 설계 문서의 구조/드래그·스냅/펼침·접힘/나가기·설정/씬 섹션이 전부 태스크로 매핑된다. "이어하기 연동"과 "버튼 위치 저장"은 설계 문서의 범위 밖 항목이라 플랜에도 포함하지 않았다.
- **타입 일관성**: `FloatingMenuLayout.SnapTargetX`/`ExpandDirectionSign`(Task 1) 시그니처가 Task 2의 `FloatingMenuButton.OnEndDrag`/`Expand`에서 그대로 쓰인다. Task 2가 선언한 직렬화 필드명(`exitButton`/`settingsButton`/`blocker`/`blockerButton`)이 Task 3의 `SerializedObject.FindProperty` 문자열과 정확히 일치한다.
- **플레이스홀더 스캔**: 없음.
