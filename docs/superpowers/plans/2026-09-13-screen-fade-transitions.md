# 화면 전환 페이드 효과 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 메인 메뉴가 처음 보일 때, 그리고 이름 확정 후 채팅창이 보일 때 즉시 나타나는 대신 0.4초에
걸쳐 서서히 페이드인하게 만든다.

**Architecture:** `ChatBootstrap`(이미 모든 패널 노출을 담당)에 `CanvasGroup` 기반 `FadeIn` 헬퍼를
추가하고, `MainMenuPanel`/`chatUIRoot`에 붙인 `CanvasGroup`을 그 헬퍼로 페이드시킨다. 채팅화면 →
메인 메뉴 복귀는 씬 리로드라서 `Start()`가 처음부터 다시 실행되므로 추가 구현이 필요 없다.

**Tech Stack:** Unity 6000.3.10f1, C# / DOTween(`DOTween.To(getter, setter, ...)` 패턴 — 이 프로젝트엔
`RectTransform.DOAnchorPos`류 DOTween UI 모듈이 없다).

## Global Constraints

- 로직은 전부 `ChatBootstrap.cs`에 둔다 — 별도 static 클래스나 컴포넌트로 빼지 않는다.
- 이전 화면은 `SetActive(false)`로 즉시 사라지고, 새 화면만 투명→불투명으로 페이드인한다
  (크로스페이드 아님).
- 페이드 지속시간은 0.4초 고정값.
- 페이드 도중엔 `blocksRaycasts = false`로 막아뒀다가 끝나면 `true`로 푼다.
- 채팅화면 → 메인 메뉴 복귀(`FloatingMenuButton.ExitToMainMenu`의 씬 리로드)에는 별도 코드를
  추가하지 않는다 — 리로드로 `Start()`가 다시 실행되며 자동으로 커버된다.
- 이 기능엔 EditMode 자동 테스트를 두지 않는다 — `ChatBootstrap`은 MonoBehaviour이고
  DOTween 애니메이션 위주라 이 프로젝트의 다른 UI 스크립트(`ChatBubbleListView`,
  `FloatingMenuButton` 등)와 같은 이유로 자동 테스트 대상이 아니다. 컴파일 확인 + 수동 Play
  모드 검증으로 마무리한다.

---

### Task 1: `ChatBootstrap`에 `FadeIn` 헬퍼와 호출부 추가

**Files:**
- Modify: `Assets/02_Script/Runtime/UI/ChatBootstrap.cs`

**Interfaces:**
- Produces: `[SerializeField] CanvasGroup mainMenuPanelGroup;`, `[SerializeField] CanvasGroup chatUIRootGroup;` — Task 2가 씬/프리팹에서 이 필드들을 연결한다. `private void FadeIn(CanvasGroup group, float duration = 0.4f)` — 이 파일 안에서만 쓰는 private 메서드.

- [ ] **Step 1: `ChatBootstrap.cs` 전체 교체**

```csharp
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
            mainMenuPanelGroup.alpha = 0f;

            mainMenuPanel.OnNewGameClicked += HandleNewGameClicked;
            mainMenuPanel.OnHistoryClicked += HandleHistoryClicked;
            keywordIntroPanel.OnKeywordsConfirmed += HandleKeywordsConfirmed;
            playerNamePanel.OnNameConfirmed += HandleNameConfirmed;
            historyPanel.OnBackClicked += HandleHistoryBackClicked;
            historyPanel.OnEntrySelected += HandleHistoryEntrySelected;

            FadeIn(mainMenuPanelGroup);
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
        // 다 나타나기 전에 버튼이 눌리는 걸 방지한다.
        private void FadeIn(CanvasGroup group, float duration = 0.4f)
        {
            group.blocksRaycasts = false;
            DOTween.To(() => group.alpha, v => group.alpha = v, 1f, duration)
                .OnComplete(() => group.blocksRaycasts = true);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`)로 재컴파일을 요청하고, `read_console`(`types: ["error"]`)로 에러가 없는지 확인한다.
Expected: 에러 0건. (`mainMenuPanelGroup`/`chatUIRootGroup`은 아직 씬/프리팹에서 연결 전이라 인스펙터에선 비어있겠지만, 컴파일 자체는 통과해야 한다.)

- [ ] **Step 3: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: 기존 테스트 전부 PASS (57개 — 이 변경은 아무 테스트도 새로 추가하지 않는다).

- [ ] **Step 4: Commit**

```bash
git add Assets/02_Script/Runtime/UI/ChatBootstrap.cs
git commit -m "feat: fade in main menu and chat screen instead of snapping to visible"
```

---

### Task 2: 씬/프리팹에 CanvasGroup 추가 + 필드 연결 + 수동 검증

**Files:**
- Modify: `Assets/01_Scene/PlayScene.unity` (`MainMenuPanel`에 `CanvasGroup` 추가, `ChatBootstrap` 인스턴스의 `mainMenuPanelGroup` 필드 연결)
- Modify: `Assets/04_Prefab/ChatUI.prefab` (`ChatUIRoot`에 `CanvasGroup` 추가, `ChatBootstrap`의 `chatUIRootGroup` 필드 연결)

**Interfaces:**
- Consumes: `TextingRPG.UI.ChatBootstrap`(Task 1)의 `mainMenuPanelGroup`/`chatUIRootGroup` 직렬화 필드.
- 이 태스크로 플랜의 마지막 산출물이 나온다 — 자동화 테스트 대상 아님, 수동 Play 모드 검증으로 마무리.

- [ ] **Step 1: 컴파일 확인 (사전 점검)**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후 `read_console`(`types: ["error"]`)로 에러 0건 확인. (Task 1 커밋 이후 도메인 리로드가 끝난 상태인지 재확인 — `ChatBootstrap`이 새 필드를 가진 채로 컴파일된 어셈블리에 있어야 다음 스텝의 `execute_code`가 참조할 수 있다.)

- [ ] **Step 2: `execute_code`로 `ChatUI.prefab`의 `ChatUIRoot`에 `CanvasGroup` 추가 + `chatUIRootGroup` 연결**

`mcp__UnityMCP__execute_code`(`action: "execute"`, `compiler: "codedom"`)로 아래 코드를 실행한다. `execute_code`의 컴파일러는 Roslyn 없이 CodeDom(C# 6)이라 `using` 지시문과 로컬 함수를 못 쓰므로 완전정규화 이름을 쓰고, 프로젝트 커스텀 타입(`ChatBootstrap`)은 `AppDomain.CurrentDomain.GetAssemblies()`로 리플렉션해서 찾는다(`FloatingMenu.prefab`을 만들 때와 같은 방식):

```csharp
System.Type chatBootstrapType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var t = asm.GetType("TextingRPG.UI.ChatBootstrap");
    if (t != null) { chatBootstrapType = t; break; }
}
if (chatBootstrapType == null) return "ChatBootstrap 타입을 로드된 어셈블리에서 찾지 못함";

string prefabPath = "Assets/04_Prefab/ChatUI.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
var chatUIRoot = root.transform.Find("ChatUIRoot");
if (chatUIRoot == null) { UnityEditor.PrefabUtility.UnloadPrefabContents(root); return "ChatUIRoot를 찾지 못함"; }

var group = chatUIRoot.GetComponent<UnityEngine.CanvasGroup>();
if (group == null) group = chatUIRoot.gameObject.AddComponent<UnityEngine.CanvasGroup>();

var bootstrapComp = root.GetComponent(chatBootstrapType);
if (bootstrapComp == null) { UnityEditor.PrefabUtility.UnloadPrefabContents(root); return "ChatBootstrap 컴포넌트를 프리팹 루트에서 찾지 못함"; }

var so = new UnityEditor.SerializedObject(bootstrapComp);
so.FindProperty("chatUIRootGroup").objectReferenceValue = group;
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);

return "chatUIRootGroup wired";
```

Expected: 반환값 `"chatUIRootGroup wired"`, 예외 없음.

- [ ] **Step 3: `execute_code`로 씬의 `MainMenuPanel`에 `CanvasGroup` 추가 + `mainMenuPanelGroup` 연결**

이어서 같은 `execute_code` 도구로 아래 코드를 실행한다. `ChatBootstrap`은 씬에서 `ChatUI` 프리팹 인스턴스의 루트 오브젝트에 붙어있다(Task 2 Step 2와 같은 `chatBootstrapType` 리플렉션을 다시 구한다 — 별도 실행이라 이전 변수는 남아있지 않음):

```csharp
System.Type chatBootstrapType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var t = asm.GetType("TextingRPG.UI.ChatBootstrap");
    if (t != null) { chatBootstrapType = t; break; }
}
if (chatBootstrapType == null) return "ChatBootstrap 타입을 로드된 어셈블리에서 찾지 못함";

UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/01_Scene/PlayScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

var mainMenuPanel = UnityEngine.GameObject.Find("MainMenuPanel");
if (mainMenuPanel == null) return "MainMenuPanel을 씬에서 찾지 못함";

var group = mainMenuPanel.GetComponent<UnityEngine.CanvasGroup>();
if (group == null) group = mainMenuPanel.AddComponent<UnityEngine.CanvasGroup>();

var chatUIGO = UnityEngine.GameObject.Find("ChatUI");
if (chatUIGO == null) return "ChatUI(프리팹 인스턴스)를 씬에서 찾지 못함";

var bootstrapComp = chatUIGO.GetComponent(chatBootstrapType);
if (bootstrapComp == null) return "ChatBootstrap 컴포넌트를 ChatUI에서 찾지 못함";

var so = new UnityEditor.SerializedObject(bootstrapComp);
so.FindProperty("mainMenuPanelGroup").objectReferenceValue = group;
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

return "mainMenuPanelGroup wired";
```

Expected: 반환값 `"mainMenuPanelGroup wired"`, 예외 없음.

- [ ] **Step 4: 컴파일/콘솔 확인**

`refresh_unity` (`mode: "force"`, `scope: "all"`, `compile: "request"`) 후 `read_console`(`types: ["error", "warning"]`)로 확인한다.
Expected: 에러 0건. (직렬화 필드 참조가 비어있으면 Play 시작할 때 NullReferenceException으로 드러난다 — 이 스텝에선 콘솔에 아직 안 나타날 수 있으니 다음 스텝의 수동 검증으로 최종 확인한다.)

- [ ] **Step 5: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (57개 전부).

- [ ] **Step 6: 수동 End-to-End 검증 (Play 모드)**

Play 모드로 진입해서 아래를 순서대로 확인한다:

1. Play를 누른 직후 메인 메뉴가 즉시 나타나지 않고 약 0.4초에 걸쳐 서서히 보이는지, 다 보이기 전에 버튼을 눌러도 반응이 없는지
2. 새 게임 → 키워드 3개 선택 → 이름 입력 → 확정 시, 채팅창이 즉시 나타나지 않고 서서히 보이는지
3. 채팅 화면에서 원형 메뉴(`FloatingMenuButton`)의 나가기(`←`)를 누르면 씬이 리로드되면서 메인 메뉴가 다시 1번처럼 페이드인하는지
4. 히스토리 화면 진입/뒤로가기, 키워드→이름입력 전환 등 이번에 손대지 않은 다른 전환들은 기존처럼 즉시 전환되는지 (의도한 대로 — 이번 범위가 아님)

- [ ] **Step 7: Commit**

```bash
git add Assets/01_Scene/PlayScene.unity Assets/04_Prefab/ChatUI.prefab
git commit -m "feat: wire CanvasGroup fade-in for main menu and chat screen"
```

---

## Self-Review Notes

- **스펙 커버리지**: `FadeIn` 헬퍼 + 두 호출 지점(Task 1), 씬/프리팹 `CanvasGroup` 추가 및 필드 연결 + 수동 검증(Task 2) — 설계 문서의 두 지점(①부팅→메인메뉴, ②이름확정→채팅창)과 "③은 씬 리로드로 자동 커버"라는 명시적 범위 설명이 전부 반영됐다.
- **타입 일관성**: Task 1이 선언한 필드명 `mainMenuPanelGroup`/`chatUIRootGroup`이 Task 2의 `SerializedObject.FindProperty` 문자열과 정확히 일치한다. `FadeIn(CanvasGroup group, float duration = 0.4f)` 시그니처가 두 호출부(`FadeIn(mainMenuPanelGroup)`, `FadeIn(chatUIRootGroup)`)에서 기본값 그대로 쓰인다.
- **플레이스홀더 스캔**: 없음.
