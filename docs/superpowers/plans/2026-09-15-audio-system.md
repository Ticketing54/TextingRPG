# 오디오 시스템(배경음악 + UI 효과음) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 배경음악(루프 1트랙)과 버튼 호버 효과음을 재생하는 `AudioManager` 싱글턴을 만들고,
`SettingsPanel`의 볼륨 슬라이더와 프로젝트 전체 버튼(동적으로 생성되는 것 포함)에 연결한다.
오디오 파일은 아직 없어서 클립 필드는 비워두고, 비어있어도 에러 없이 조용히 넘어가게 만든다.

**Architecture:** `AudioManager`는 `DataManager`와 동일한 패턴의 전역 싱글턴(씬에 배치된 평범한
MonoBehaviour, `DontDestroyOnLoad` 없음)이다. 호버 효과음은 별도의 작은 `ButtonHoverSound`
컴포넌트가 담당하고, `AudioManager`가 부팅 시 씬의 모든 `Button`에 자동으로 붙여준다.

**Tech Stack:** Unity 6000.3.10f1, C# / `UnityEngine.AudioSource`, `UnityEngine.UI.Button`.

## Global Constraints

- 범위는 UI 효과음(버튼 호버)과 배경음악(하나로 루프)뿐이다. 나레이션 타이핑 효과음은 빼기로 했다
  ("카톡 알림 같은 느낌이라 안 어울린다").
- `AudioManager`는 `DataManager`와 같은 싱글턴 패턴(`public static AudioManager Instance`,
  `DontDestroyOnLoad` 없음)을 그대로 따른다.
- `bgmClip`/`hoverSound` 클립 필드는 비워둔 채로 커밋한다 — 사용자가 나중에 오디오 파일을 인스펙터에
  직접 연결한다. 클립이 `null`이면 재생 시도 없이 조용히 넘어가야 한다(에러 금지).
- `SettingsPanel`의 볼륨 슬라이더는 `ChatBootstrap`을 거치지 않고 `AudioManager.Instance`를
  직접 호출한다(`AudioManager`도 `DataManager`처럼 전역 싱글턴이라 바로 접근 가능 — 타이핑
  속도처럼 이벤트로 한 겹 더 감쌀 이유가 없음).
- `AudioManager.RegisterHoverSounds()`는 이미 `ButtonHoverSound`가 붙어있는 버튼은 건너뛰어서
  여러 번 호출해도 안전해야 한다.
- 이 기능엔 EditMode 자동 테스트를 두지 않는다 — MonoBehaviour + Unity 오디오/이벤트 API 위주라
  이 프로젝트의 다른 UI 스크립트들과 같은 이유로 자동 테스트 대상이 아니다. 컴파일 확인 + 수동
  Play 모드 검증으로 마무리한다.

---

### Task 1: `AudioManager` + `ButtonHoverSound` 작성

**Files:**
- Create: `Assets/02_Script/Runtime/Systems/AudioManager.cs`
- Create: `Assets/02_Script/Runtime/UI/ButtonHoverSound.cs`

**Interfaces:**
- Produces: `TextingRPG.Systems.AudioManager.Instance`(static), `SetBgmVolume(float)`,
  `SetSfxVolume(float)`, `PlayHoverSound()`, `RegisterHoverSounds()` — Task 2가 이 넷을 소비한다.
  `[SerializeField] AudioSource bgmSource;`, `[SerializeField] AudioSource sfxSource;`,
  `[SerializeField] AudioClip bgmClip;`, `[SerializeField] AudioClip hoverSound;` — Task 3에서
  씬 오브젝트에 연결한다.

- [ ] **Step 1: `ButtonHoverSound.cs` 작성**

```csharp
using TextingRPG.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TextingRPG.UI
{
    // AudioManager.RegisterHoverSounds()가 씬의 모든 Button에 자동으로 붙인다 — 직접 붙일 필요 없다.
    public class ButtonHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => AudioManager.Instance?.PlayHoverSound();
    }
}
```

- [ ] **Step 2: `AudioManager.cs` 작성**

```csharp
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
```

- [ ] **Step 3: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "all"`, `compile: "request"`)로 재컴파일을 요청하고
(새 파일이라 `scope: "all"`로 에셋 임포트까지 반영), `read_console`(`types: ["error"]`)로 에러가
없는지 확인한다.
Expected: 에러 0건. (`bgmSource`/`sfxSource`는 아직 씬에서 연결 전이라 Play 시 `AudioManager`가
활성화되면 예외가 나겠지만, 이 스텝은 컴파일만 확인한다. Task 3에서 해결된다.)

- [ ] **Step 4: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: 기존 테스트 전부 PASS (57개 — 이 변경은 아무 테스트도 새로 추가하지 않는다).

- [ ] **Step 5: Commit**

```bash
git add Assets/02_Script/Runtime/Systems/AudioManager.cs Assets/02_Script/Runtime/UI/ButtonHoverSound.cs
git commit -m "feat: add AudioManager singleton for BGM and button hover SFX"
```

---

### Task 2: `SettingsPanel`/`KeywordIntroPanel`/`HistoryPanel` 연결

**Files:**
- Modify: `Assets/02_Script/Runtime/UI/SettingsPanel.cs`
- Modify: `Assets/02_Script/Runtime/UI/KeywordIntroPanel.cs`
- Modify: `Assets/02_Script/Runtime/UI/HistoryPanel.cs`

**Interfaces:**
- Consumes: `TextingRPG.Systems.AudioManager.Instance`, `.SetBgmVolume(float)`,
  `.SetSfxVolume(float)`, `.RegisterHoverSounds()` (Task 1).

- [ ] **Step 1: `SettingsPanel.cs` 수정**

`using` 목록에 `TextingRPG.Systems`를 추가한다:

```csharp
using System;
using TextingRPG.Systems;
using UnityEngine;
using UnityEngine.UI;
```

`Awake()`를 아래로 교체한다:

```csharp
        private void Awake()
        {
            backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
            typingSpeedSlider.onValueChanged.AddListener(value => OnTypingSpeedChanged?.Invoke(value));
            bgmVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetBgmVolume(value));
            sfxVolumeSlider.onValueChanged.AddListener(value => AudioManager.Instance?.SetSfxVolume(value));
        }
```

- [ ] **Step 2: `KeywordIntroPanel.cs` 수정**

`using` 목록에 `TextingRPG.Systems`를 추가한다:

```csharp
using System;
using System.Collections.Generic;
using TextingRPG.Core;
using TextingRPG.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
```

`Awake()`의 키워드 버튼 생성 루프 뒤, `startButton` 배선 앞에 한 줄 추가한다:

```csharp
        private void Awake()
        {
            foreach (var keyword in keywordSet.Keywords)
            {
                var button = Instantiate(keywordButtonPrefab, keywordButtonContainer);
                button.GetComponentInChildren<TMP_Text>().text = keyword;
                button.onClick.AddListener(() => ToggleKeyword(keyword, button));
            }

            AudioManager.Instance?.RegisterHoverSounds();

            startButton.onClick.AddListener(ConfirmSelection);
            startButton.interactable = false;
        }
```

- [ ] **Step 3: `HistoryPanel.cs` 수정**

`using` 목록에 `TextingRPG.Systems`를 추가한다(이미 `TextingRPG.Systems`가 `SaveSystem` 때문에
들어있으므로 실제로는 추가할 게 없다 — 아래처럼 그대로 쓸 수 있는지만 확인):

```csharp
using System;
using System.Globalization;
using TextingRPG.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
```

`Populate()`의 마지막(`foreach` 루프 뒤)에 한 줄 추가한다:

```csharp
        public void Populate()
        {
            for (int i = entryContainer.childCount - 1; i >= 0; i--)
                Destroy(entryContainer.GetChild(i).gameObject);

            var entries = SaveSystem.LoadHistoryIndex().Entries;
            if (emptyLabel != null) emptyLabel.SetActive(entries.Count == 0);

            foreach (var entry in entries)
            {
                var button = Instantiate(entryButtonPrefab, entryContainer);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = FormatEntry(entry);

                var id = entry.Id;
                button.onClick.AddListener(() => OnEntrySelected?.Invoke(id));
            }

            AudioManager.Instance?.RegisterHoverSounds();
        }
```

- [ ] **Step 4: 컴파일 확인**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후
`read_console`(`types: ["error"]`)로 에러 0건 확인.

- [ ] **Step 5: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (57개 전부).

- [ ] **Step 6: Commit**

```bash
git add Assets/02_Script/Runtime/UI/SettingsPanel.cs Assets/02_Script/Runtime/UI/KeywordIntroPanel.cs Assets/02_Script/Runtime/UI/HistoryPanel.cs
git commit -m "feat: wire volume sliders and dynamic-button hover SFX to AudioManager"
```

---

### Task 3: 씬에 `AudioManager` 배치 + 수동 검증

**Files:**
- Modify: `Assets/01_Scene/PlayScene.unity`

**Interfaces:**
- Consumes: `TextingRPG.Systems.AudioManager`(Task 1)의 `bgmSource`/`sfxSource` 직렬화 필드.
- 이 태스크로 플랜의 마지막 산출물이 나온다 — 자동화 테스트 대상 아님, 수동 Play 모드 검증으로 마무리.

- [ ] **Step 1: 컴파일 확인 (사전 점검)**

`refresh_unity` (`mode: "force"`, `scope: "scripts"`, `compile: "request"`) 후
`read_console`(`types: ["error"]`)로 에러 0건 확인 — Task 1/2가 컴파일된 어셈블리에 반영된
상태인지 재확인.

- [ ] **Step 2: `execute_code`로 `AudioManager` GameObject 생성 + 배선**

`mcp__UnityMCP__execute_code`(`action: "execute"`, `compiler: "codedom"`)로 아래 코드를 실행한다.
`AudioManager` 타입은 프로젝트 커스텀 타입이라 `AppDomain.CurrentDomain.GetAssemblies()`로
리플렉션해서 찾는다(기존 컨벤션과 동일):

```csharp
System.Type audioManagerType = null;
foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
{
    var t = asm.GetType("TextingRPG.Systems.AudioManager");
    if (t != null) { audioManagerType = t; break; }
}
if (audioManagerType == null) return "AudioManager 타입을 로드된 어셈블리에서 찾지 못함";

UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/01_Scene/PlayScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

var existing = UnityEngine.GameObject.Find("AudioManager");
if (existing != null) return "AudioManager가 이미 씬에 있음 — 중복 생성 방지, 건너뜀";

var go = new UnityEngine.GameObject("AudioManager");
var bgmSource = go.AddComponent<UnityEngine.AudioSource>();
bgmSource.loop = true;
bgmSource.playOnAwake = false;
var sfxSource = go.AddComponent<UnityEngine.AudioSource>();
sfxSource.loop = false;
sfxSource.playOnAwake = false;

var audioManagerComp = go.AddComponent(audioManagerType);
var so = new UnityEditor.SerializedObject(audioManagerComp);
so.FindProperty("bgmSource").objectReferenceValue = bgmSource;
so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

return "AudioManager created and wired";
```

Expected: 반환값 `"AudioManager created and wired"`, 예외 없음.

- [ ] **Step 3: 컴파일/콘솔 확인**

`refresh_unity` (`mode: "force"`, `scope: "all"`, `compile: "request"`) 후
`read_console`(`types: ["error", "warning"]`)로 확인한다.
Expected: 에러 0건.

- [ ] **Step 4: 전체 EditMode 테스트 실행 (회귀 확인)**

`run_tests` (`mode: "EditMode"`)를 필터 없이 실행한다.
Expected: PASS (57개 전부).

- [ ] **Step 5: 수동 End-to-End 검증 (Play 모드)**

Play 모드로 진입해서 아래를 순서대로 확인한다(클립이 없어서 소리는 안 나지만, 에러 없이
조용히 넘어가는지가 핵심이다):

1. Play를 눌렀을 때 `AudioManager` 관련 예외(`NullReferenceException` 등)가 콘솔에 안 뜨는지
2. 메인 메뉴/설정/히스토리 화면의 버튼 위에 마우스를 올려도(호버) 에러 없이 넘어가는지
3. 새 게임 → 키워드 선택 화면에서 키워드 버튼들 위에 마우스를 올려도 에러 없는지 (동적 생성
   버튼도 `RegisterHoverSounds()`로 잡혔는지 확인하는 부분)
4. 히스토리 화면에 완료작이 있다면, 그 항목 버튼 위에 마우스를 올려도 에러 없는지
5. 설정 화면의 배경음악/효과음 슬라이더를 움직여도 에러 없이 넘어가는지
6. (선택) 오디오 파일이 있다면 `AudioManager`의 `bgmClip`/`hoverSound`에 인스펙터로 연결해보고
   실제로 소리가 나는지, 슬라이더로 볼륨이 조절되는지 확인 — 지금 이 플랜 범위 밖이라 파일이
   없으면 건너뛴다

- [ ] **Step 6: Commit**

```bash
git add Assets/01_Scene/PlayScene.unity
git commit -m "feat: place AudioManager in PlayScene and wire its AudioSources"
```

---

## Self-Review Notes

- **스펙 커버리지**: `AudioManager`/`ButtonHoverSound`(Task 1), `SettingsPanel` 볼륨 연결 +
  `KeywordIntroPanel`/`HistoryPanel` 동적 버튼 보완(Task 2), 씬 배치 + 수동 검증(Task 3) — 설계
  문서의 모든 섹션이 태스크로 매핑된다. "범위 밖" 항목(타이핑 효과음, 오디오 파일 자체, 트랙 분리,
  설정값 저장)은 플랜에도 포함하지 않았다.
- **타입 일관성**: Task 1이 선언한 `AudioManager.Instance`/`SetBgmVolume`/`SetSfxVolume`/
  `PlayHoverSound`/`RegisterHoverSounds`가 Task 2의 세 파일에서 그대로 쓰인다. Task 1의
  직렬화 필드명 `bgmSource`/`sfxSource`가 Task 3의 `SerializedObject.FindProperty` 문자열과
  정확히 일치한다.
- **플레이스홀더 스캔**: 없음.
