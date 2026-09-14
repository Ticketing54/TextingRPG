# 메인 메뉴 프리팹화 + 타이틀 타이핑/버튼 등장 애니메이션 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 메인 메뉴가 처음 뜰 때 타이틀 텍스트가 채팅 말풍선처럼 타이핑되고, 그 직후 버튼 5개가
새게임→이어하기→히스토리→설정→종료 순서로 시간차를 두고 나타나게 만든다. 겸사겸사
`MainMenuPanel`을 `FloatingMenu`처럼 독립 프리팹으로 뽑아낸다.

**Architecture:** `MainMenuPanel.cs`가 자기 타이틀 타이핑과 버튼 순차 등장을 전담하는
`PlayIntroAnimation()`을 갖는다. `ChatBootstrap`은 기존 `FadeIn` 헬퍼에 완료 콜백을 추가해서,
메인 메뉴 패널이 다 페이드인된 직후 이 메서드를 한 번 호출한다.

**Tech Stack:** Unity 6000.3.10f1, C# / DOTween(`DOTween.To(getter, setter, ...)` 패턴 —
`ChatBubble.cs`의 타이핑 애니메이션과 동일한 방식).

## Global Constraints

- `MainMenuPanel`을 `Assets/04_Prefab/MainMenuPanel.prefab`로 뽑아낸다(`FloatingMenu.prefab`과
  같은 방식 — `PrefabUtility.SaveAsPrefabAssetAndConnect`).
- 타이틀 타이핑은 `ChatBubble.cs`와 동일한 패턴(`revealed` int를 `DOTween.To`로 증가시키고
  `OnUpdate`에서 `maxVisibleCharacters`에 반영)을 그대로 가져온다.
- 버튼 5개 각각에 `CanvasGroup`을 붙여서 처음엔 숨겨두고(`alpha=0`, `blocksRaycasts=false`),
  타이틀 타이핑이 끝나면 새게임→이어하기→히스토리→설정→종료 순서로 시간차(`buttonRevealStagger`,
  기본 0.15초)를 두고 페이드인한다. 각자 페이드가 끝나면 `blocksRaycasts=true`로 풀린다.
- `이어하기`/`설정`은 기존처럼 `interactable=false` 그대로 유지 — 페이드인은 되지만 눌리지 않는다.
- 이 인트로 애니메이션은 앱을 처음 켰을 때(`ChatBootstrap.Start()`) **딱 한 번만** 재생된다.
  히스토리에서 뒤로가기로 돌아올 때는 다시 재생하지 않는다 — 별도 코드 없이, 첫 재생이 끝난 뒤
  타이틀/버튼이 "다 보이는" 상태로 남아있는 걸 그대로 이용한다.
- 이 기능엔 EditMode 자동 테스트를 두지 않는다 — MonoBehaviour + DOTween 애니메이션 위주라
  이 프로젝트의 다른 UI 스크립트들과 같은 이유. 컴파일 확인 + 수동 Play 모드 검증으로 마무리한다.
- 프리팹/씬 구성은 `execute_code` 도구로 편집기 스크립트를 실행해 처리한다(기존 컨벤션).

---

### Task 1: `MainMenuPanel`에 타이틀 타이핑 + 버튼 등장 로직 추가

**Files:**
- Modify: `Assets/02_Script/Runtime/UI/MainMenuPanel.cs`

**Interfaces:**
- Produces: `[SerializeField] TMP_Text titleLabel;`(Task 3에서 씬의 `TitleLabel`을 연결), `public void PlayIntroAnimation()`(Task 2의 `ChatBootstrap`이 호출) — 기존 5개 버튼 필드(`newGameButton`/`continueButton`/`historyButton`/`settingsButton`/`quitButton`)는 그대로 두되, 각 버튼의 `CanvasGroup` 컴포넌트가 있어야 한다(Task 3에서 붙인다).

- [ ] **Step 1: `MainMenuPanel.cs` 전체 교체**

```csharp
using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 부팅 시 첫 화면. 새 게임만 동작하고, 세이브 시스템이 붙기 전까지
    // 이어하기/히스토리/설정은 비활성 자리표시자로 둔다.
    // 앱을 처음 켰을 때 한 번, ChatBootstrap이 PlayIntroAnimation()을 불러서
    // 타이틀이 타이핑되고 버튼이 순서대로 나타나는 연출을 재생한다.
    public class MainMenuPanel : MonoBehaviour
    {
        [SerializeField] Button newGameButton;
        [SerializeField] Button continueButton;
        [SerializeField] Button historyButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] float titleCharsPerSecond = 30f;
        [SerializeField] float buttonRevealDuration = 0.25f;
        [SerializeField] float buttonRevealStagger = 0.15f;

        public event Action OnNewGameClicked;
        public event Action OnHistoryClicked;

        private string _fullTitle;
        private CanvasGroup[] _buttonGroupsInRevealOrder;

        private void Awake()
        {
            newGameButton.onClick.AddListener(() => OnNewGameClicked?.Invoke());
            historyButton.onClick.AddListener(() => OnHistoryClicked?.Invoke());
            quitButton.onClick.AddListener(Quit);

            // 이어하기는 실제 재개 로직이 붙기 전까지, 설정은 조절할 항목이 생기기 전까지 비활성.
            continueButton.interactable = false;
            settingsButton.interactable = false;

            _buttonGroupsInRevealOrder = new[]
            {
                newGameButton.GetComponent<CanvasGroup>(),
                continueButton.GetComponent<CanvasGroup>(),
                historyButton.GetComponent<CanvasGroup>(),
                settingsButton.GetComponent<CanvasGroup>(),
                quitButton.GetComponent<CanvasGroup>(),
            };

            _fullTitle = titleLabel.text;
            titleLabel.text = "";
            titleLabel.maxVisibleCharacters = 0;

            foreach (var group in _buttonGroupsInRevealOrder)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
            }
        }

        // 앱을 처음 켰을 때 딱 한 번, ChatBootstrap이 메인 메뉴 패널 페이드인 직후에 호출한다.
        // 타이틀이 다 타이핑되면 버튼들이 순서대로 나타나고, 이후엔 그 상태가 계속 유지된다
        // (히스토리에서 뒤로가기로 돌아와도 다시 재생되지 않음 — 이미 다 보이는 상태 그대로 남아있다).
        public void PlayIntroAnimation()
        {
            int revealed = 0;
            float duration = Mathf.Max(0.01f, _fullTitle.Length / titleCharsPerSecond);
            DOTween.To(() => revealed, v => revealed = v, _fullTitle.Length, duration)
                .SetEase(Ease.Linear)
                .OnUpdate(() => titleLabel.maxVisibleCharacters = revealed)
                .OnComplete(RevealButtons);
        }

        private void RevealButtons()
        {
            for (int i = 0; i < _buttonGroupsInRevealOrder.Length; i++)
            {
                var group = _buttonGroupsInRevealOrder[i];
                DOTween.To(() => group.alpha, v => group.alpha = v, 1f, buttonRevealDuration)
                    .SetDelay(i * buttonRevealStagger)
                    .OnComplete(() => group.blocksRaycasts = true);
            }
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
```

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`)로 재컴파일을 요청하고, `read_console`(`types: ["error"]`)로 에러가 없는지 확인한다.
Expected: 에러 0건. (`titleLabel`은 아직 씬에서 연결 전이라 인스펙터에선 비어있고, 버튼들에도 아직 `CanvasGroup`이 없어서 Play를 누르면 `Awake()`의 `GetComponent<CanvasGroup>()`이 `null`을 반환해 곧바로 예외가 나겠지만 — 이 스텝은 컴파일만 확인한다. Task 3에서 해결된다.)

- [ ] **Step 3: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: 기존 테스트 전부 PASS (57개 — 이 변경은 아무 테스트도 새로 추가하지 않는다).

- [ ] **Step 4: Commit**

```bash
git add Assets/02_Script/Runtime/UI/MainMenuPanel.cs
git commit -m "feat: MainMenuPanel types out title then reveals buttons in sequence"
```

---

### Task 2: `ChatBootstrap.FadeIn`에 완료 콜백 추가

**Files:**
- Modify: `Assets/02_Script/Runtime/UI/ChatBootstrap.cs`

**Interfaces:**
- Consumes: `TextingRPG.UI.MainMenuPanel.PlayIntroAnimation()` (Task 1).
- Produces: `private void FadeIn(CanvasGroup group, System.Action onComplete = null, float duration = 0.4f)` — 기존 `FadeIn(chatUIRootGroup)` 호출은 인자 순서상 콜백 자리를 건너뛸 수 없으므로 그대로 `FadeIn(chatUIRootGroup)`로 남겨도 `onComplete`가 기본값 `null`이라 문제없다.

- [ ] **Step 1: `ChatBootstrap.cs` 수정**

`using System;`을 다른 `using` 아래에 추가한다:

```csharp
using System;
using DG.Tweening;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.Systems;
using UnityEngine;
```

`FadeIn(mainMenuPanelGroup);` 호출부(`Start()` 마지막 줄)를 아래로 교체한다:

```csharp
            FadeIn(mainMenuPanelGroup, () => mainMenuPanel.PlayIntroAnimation());
```

`FadeIn` 메서드 전체를 아래로 교체한다:

```csharp
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
```

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후 `read_console`(`types: ["error"]`)로 에러 0건 확인.

- [ ] **Step 3: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (57개 전부).

- [ ] **Step 4: Commit**

```bash
git add Assets/02_Script/Runtime/UI/ChatBootstrap.cs
git commit -m "feat: play main menu intro animation after its fade-in completes"
```

---

### Task 3: `MainMenuPanel` 프리팹화 + 버튼 `CanvasGroup` 추가 + `titleLabel` 연결 + 수동 검증

**Files:**
- Create: `Assets/04_Prefab/MainMenuPanel.prefab` (+ `.meta`)
- Modify: `Assets/01_Scene/PlayScene.unity`

**Interfaces:**
- Consumes: `TextingRPG.UI.MainMenuPanel`(Task 1)의 `titleLabel` 직렬화 필드, 5개 버튼 GameObject(`NewGameButton`/`ContinueButton`/`HistoryButton`/`SettingsButton`/`QuitButton`, 이미 씬에 존재).
- 이 태스크로 플랜의 마지막 산출물이 나온다 — 자동화 테스트 대상 아님, 수동 Play 모드 검증으로 마무리.

- [ ] **Step 1: 컴파일 확인 (사전 점검)**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후 `read_console`(`types: ["error"]`)로 에러 0건 확인 — Task 1/2가 컴파일된 어셈블리에 반영된 상태인지 재확인.

- [ ] **Step 2: `execute_code`로 버튼 `CanvasGroup` 추가 + `titleLabel` 연결 + 프리팹 추출**

`mcp__UnityMCP__execute_code`(`action: "execute"`, `compiler: "codedom"`)로 아래 코드를 실행한다. `MainMenuPanel`은 씬의 일반 오브젝트(어떤 프리팹의 자식도 아님)이므로 `FloatingMenu.prefab`을 만들 때처럼 `LoadPrefabContents`가 필요 없고, `PrefabUtility.SaveAsPrefabAssetAndConnect`로 직접 뽑아낸다:

```csharp
System.Type mainMenuPanelType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var t = asm.GetType("TextingRPG.UI.MainMenuPanel");
    if (t != null) { mainMenuPanelType = t; break; }
}
if (mainMenuPanelType == null) return "MainMenuPanel 타입을 로드된 어셈블리에서 찾지 못함";

UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/01_Scene/PlayScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

var panelGO = UnityEngine.GameObject.Find("MainMenuPanel");
if (panelGO == null) return "MainMenuPanel을 씬에서 찾지 못함";

string[] buttonNames = { "NewGameButton", "ContinueButton", "HistoryButton", "SettingsButton", "QuitButton" };
foreach (var name in buttonNames)
{
    var bt = panelGO.transform.Find(name);
    if (bt == null) return "버튼을 찾지 못함: " + name;
    if (bt.GetComponent<UnityEngine.CanvasGroup>() == null)
        bt.gameObject.AddComponent<UnityEngine.CanvasGroup>();
}

var titleTransform = panelGO.transform.Find("TitleLabel");
if (titleTransform == null) return "TitleLabel을 찾지 못함";
var titleTmp = titleTransform.GetComponent<TMPro.TMP_Text>();
if (titleTmp == null) return "TitleLabel에 TMP_Text 계열 컴포넌트가 없음";

var panelComp = panelGO.GetComponent(mainMenuPanelType);
if (panelComp == null) return "MainMenuPanel 컴포넌트를 찾지 못함";

var so = new UnityEditor.SerializedObject(panelComp);
so.FindProperty("titleLabel").objectReferenceValue = titleTmp;
so.ApplyModifiedPropertiesWithoutUndo();

string prefabPath = "Assets/04_Prefab/MainMenuPanel.prefab";
bool success;
UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(panelGO, prefabPath, UnityEditor.InteractionMode.AutomatedAction, out success);
if (!success) return "SaveAsPrefabAssetAndConnect 실패";

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

return "MainMenuPanel extracted and wired";
```

Expected: 반환값 `"MainMenuPanel extracted and wired"`, 예외 없음.

- [ ] **Step 3: 컴파일/콘솔 확인**

`refresh_unity` (`mode: "force"`, `scope: "all"`, `compile: "request"`) 후 `read_console`(`types: ["error", "warning"]`)로 확인한다.
Expected: 에러 0건.

- [ ] **Step 4: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (57개 전부).

- [ ] **Step 5: 수동 End-to-End 검증 (Play 모드)**

Play 모드로 진입해서 아래를 순서대로 확인한다:

1. 메인 메뉴 패널이 페이드인된 뒤, 타이틀이 즉시 다 보이지 않고 글자가 하나씩 타이핑되는지
2. 타이핑이 끝난 뒤 버튼 5개가 새게임→이어하기→히스토리→설정→종료 순서로 시간차를 두고 나타나는지
3. 버튼이 다 나타나기 전엔 눌러도 반응이 없는지
4. 히스토리 화면으로 들어갔다가 뒤로가기로 메인 메뉴에 돌아오면, 타이핑/버튼 등장 애니메이션 없이 즉시 이미 다 보이는 상태로 나타나는지
5. `이어하기`/`설정` 버튼은 다 나타난 뒤에도 여전히 눌리지 않는지(자리표시자 상태 유지)
6. `Assets/04_Prefab/MainMenuPanel.prefab`을 프로젝트 창에서 더블클릭하면 단독으로 열려서 수정 가능한지

- [ ] **Step 6: Commit**

```bash
git add Assets/04_Prefab/MainMenuPanel.prefab Assets/04_Prefab/MainMenuPanel.prefab.meta Assets/01_Scene/PlayScene.unity
git commit -m "feat: extract MainMenuPanel into its own prefab, wire intro animation fields"
```

---

## Self-Review Notes

- **스펙 커버리지**: 프리팹화(Task 3), 타이틀 타이핑(Task 1), 버튼 순차 등장(Task 1), 재생 시점(딱 한 번, `FadeIn` 콜백 체이닝 — Task 2), `FadeIn` 시그니처 변경(Task 2) — 설계 문서의 4개 섹션이 전부 태스크로 매핑된다.
- **타입 일관성**: Task 1이 선언한 `PlayIntroAnimation()`(인자 없음, `void`)을 Task 2가 `() => mainMenuPanel.PlayIntroAnimation()`로 그대로 호출한다. Task 1의 `titleLabel`(`TMP_Text` 타입) 필드명이 Task 3의 `SerializedObject.FindProperty("titleLabel")` 문자열과 일치하고, 할당하는 `titleTmp` 변수도 `TMPro.TMP_Text`를 상속하는 구체 타입이라 대입 가능하다.
- **플레이스홀더 스캔**: 없음.
