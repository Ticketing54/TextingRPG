# 100턴 이야기 마무리 + 시작 전 키워드 선택 설계

## 개요

두 가지를 추가한다:

1. **100턴 안에 이야기가 끝나도록**: 플레이어 전송 횟수를 세서, 80턴부터 시스템 프롬프트로 마무리를 유도하고, 100턴째는 LLM에게 "이번이 마지막 턴이니 확실히 마무리하라"고 지시해 그 자리에서 결말을 자유롭게 지어내게 한다. 고정된 결말 콘텐츠는 미리 작성하지 않는다 — 그동안의 대화 흐름을 바탕으로 매 플레이마다 다른 결말이 나온다.
2. **시작 전 키워드 선택 화면**: 채팅이 시작되기 전에, 미리 준비된 키워드 6개 중 정확히 3개를 플레이어가 골라야 "모험 시작" 버튼이 활성화된다. 고른 키워드는 `worldDescription`에 덧붙어 그 판의 분위기에 반영된다.

## 범위 밖

- 여러 NPC/여러 스토리그래프에 걸친 턴 카운트 합산 — 지금처럼 NPC 1명(게르트) 기준
- 키워드 목록을 에디터 밖에서(런타임에) 편집하는 기능 — 지금은 코드/에셋에 배열로 고정, 나중에 늘릴 때도 배열에 항목만 추가
- 대화 종료 후 재시작/새 게임 흐름 — `OnConversationEnded` 이후 입력을 잠그기만 하고, "다시 시작" 버튼 등은 다루지 않는다
- 429 재시도 백오프 — 계속 보류 상태, 이번 스펙과 무관

## 기능 1: 100턴 이야기 마무리

### `PlayerState`에 턴 카운트 추가

```csharp
private readonly Dictionary<string, int> _turnCounts = new Dictionary<string, int>();

public int GetTurnCount(string npcId) =>
    _turnCounts.TryGetValue(npcId, out var value) ? value : 0;

public void IncrementTurnCount(string npcId) =>
    _turnCounts[npcId] = GetTurnCount(npcId) + 1;
```

기존 `_relationships`/`_stats`/`_conversationHistories`/`_storyNodes`와 같은 패턴(npcId 기준 dict).

### `SystemPromptBuilder`에 선택적 힌트 파라미터 추가

```csharp
public static string Build(NPCDefinition npc, string worldDescription, StoryNode currentNode, string endingHint = "")
```

`endingHint`가 비어있지 않으면 프롬프트 끝에 별도 문단으로 추가한다. `SystemPromptBuilder`는 턴 숫자나 임계값을 전혀 모른다 — 그냥 주어진 힌트 문자열을 넣을 뿐이다. 어떤 힌트를 줄지는 `ChatController`가 결정한다. 기존 3-인자 호출부(테스트 포함)는 기본값 덕분에 그대로 컴파일된다.

### `ChatController`

```csharp
private const int WrapUpTurnThreshold = 80;
private const int MaxTurns = 100;

public event Action OnConversationEnded;

private bool _conversationEnded;
```

`SendPlayerMessage` 시작 부분에 가드 추가:

```csharp
if (_conversationEnded) return;
```

플레이어 메시지를 히스토리에 추가한 직후 턴 카운트 증가:

```csharp
_playerState.IncrementTurnCount(_npc.NpcId);
int turnCount = _playerState.GetTurnCount(_npc.NpcId);
bool isFinalTurn = turnCount >= MaxTurns;
```

시스템 프롬프트를 만들 때 힌트를 계산해서 넘긴다:

```csharp
string endingHint = "";
if (isFinalTurn)
    endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
else if (turnCount >= WrapUpTurnThreshold)
    endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription, currentNode, endingHint);
```

`onSuccess` 콜백 마지막에 추가:

```csharp
if (isFinalTurn)
{
    _conversationEnded = true;
    OnConversationEnded?.Invoke();
}
```

태그 기반 노드 전이(`StoryProgression.Resolve`)는 손대지 않는다 — 마지막 턴이라고 특별 취급하지 않고 평소처럼 처리한다(결과가 의미 없어질 뿐, 해가 되진 않는다).

### `ChatBootstrap`

`OnConversationEnded` 구독 추가:

```csharp
_controller.OnConversationEnded += HandleConversationEnded;
```

```csharp
private void HandleConversationEnded()
{
    CancelInvoke(nameof(EnableSendButtonNow)); // 대기 중이던 재활성화 예약 취소
    sendButton.interactable = false;
    inputField.interactable = false;
}
```

## 기능 2: 시작 전 키워드 선택 화면

### 새 컴포넌트 `KeywordIntroPanel`

```csharp
public class KeywordIntroPanel : MonoBehaviour
{
    [SerializeField] string[] availableKeywords =
        { "안개", "폭풍우", "낡은 지도", "은화", "늑대 울음", "별빛" };
    [SerializeField] Transform keywordButtonContainer;
    [SerializeField] Button keywordButtonPrefab;
    [SerializeField] Button startButton;
    [SerializeField] int requiredSelectionCount = 3;

    public event Action<string[]> OnKeywordsConfirmed;

    private readonly List<string> _selected = new List<string>();
    private readonly Dictionary<string, Button> _buttonsByKeyword = new Dictionary<string, Button>();

    private void Awake()
    {
        foreach (var keyword in availableKeywords)
        {
            var button = Instantiate(keywordButtonPrefab, keywordButtonContainer);
            button.GetComponentInChildren<TMP_Text>().text = keyword;
            button.onClick.AddListener(() => ToggleKeyword(keyword, button));
            _buttonsByKeyword[keyword] = button;
        }
        startButton.onClick.AddListener(ConfirmSelection);
        startButton.interactable = false;
    }

    private void ToggleKeyword(string keyword, Button button)
    {
        if (_selected.Contains(keyword))
        {
            _selected.Remove(keyword);
            SetButtonSelectedVisual(button, false);
        }
        else if (_selected.Count < requiredSelectionCount)
        {
            _selected.Add(keyword);
            SetButtonSelectedVisual(button, true);
        }
        startButton.interactable = _selected.Count == requiredSelectionCount;
    }

    private void SetButtonSelectedVisual(Button button, bool selected)
    {
        var colors = button.colors;
        colors.normalColor = selected ? new Color(0.75f, 0.85f, 1f) : Color.white;
        button.colors = colors;
    }

    private void ConfirmSelection()
    {
        OnKeywordsConfirmed?.Invoke(_selected.ToArray());
        gameObject.SetActive(false);
    }
}
```

`availableKeywords`가 인스펙터에 노출된 배열이라, 나중에 항목을 추가하면 버튼도 자동으로 늘어난다(버튼을 씬에 하나씩 미리 배치해두지 않는다).

### 씬 구성

- `ChatUI` 아래에 `KeywordIntroPanel` 오브젝트 추가 (배경 + 키워드 버튼이 들어갈 빈 컨테이너 + "모험 시작" 버튼)
- 키워드 버튼 프리팹 하나(TMP 텍스트 라벨이 있는 기본 Button) 준비
- 기존 채팅 UI(Scroll View, MessageInputField, SendButton)를 감싸는 부모 오브젝트를 하나 두어 `chatUIRoot`로 지정 — 시작 전엔 꺼두고, 키워드 확정 시 켠다

### `ChatBootstrap` 재구성

`Start()`에서 바로 `ChatController`를 만들던 걸, 키워드 확정 시점으로 미룬다:

```csharp
[SerializeField] KeywordIntroPanel keywordIntroPanel;
[SerializeField] GameObject chatUIRoot;

private string _apiKey;

private void Start()
{
    _apiKey = LocalSecrets.GetGeminiApiKey();
    if (string.IsNullOrEmpty(_apiKey)) return;

    chatUIRoot.SetActive(false);
    keywordIntroPanel.OnKeywordsConfirmed += HandleKeywordsConfirmed;
}

private void HandleKeywordsConfirmed(string[] keywords)
{
    var finalWorldDescription = worldDescription + "\n\n[이번 모험의 키워드] " + string.Join(", ", keywords);

    var playerState = new PlayerState();
    ILLMProvider provider = new GeminiProvider(_apiKey, geminiModel);

    _controller = new ChatController(playerState, provider, npc, finalWorldDescription, storyGraph);
    _controller.OnError += HandleError;
    _controller.OnConversationEnded += HandleConversationEnded;
    chatBubbleListView.Bind(_controller);
    chatBubbleListView.OnMessagePlaybackComplete += HandleMessagePlaybackComplete;

    sendButton.onClick.AddListener(SendCurrentInput);
    inputField.onSubmit.AddListener(_ => SendCurrentInput());

    chatUIRoot.SetActive(true);
}
```

API 키 확인은 그대로 `Start()`에서 제일 먼저 한다 — 키워드 화면을 다 채운 뒤에야 키가 없다는 걸 알게 되는 상황을 피한다.

## 테스트 영향

- `PlayerState`: `GetTurnCount`/`IncrementTurnCount` 새 테스트를 기존 `Assets/02_Script/Tests/EditMode/PlayerStateTests.cs`에 추가
- `SystemPromptBuilderTests`: `endingHint` 파라미터가 비어있을 때 기존 동작 그대로인지(회귀), 채워졌을 때 프롬프트에 포함되는지 테스트 추가
- `ChatControllerTests`: 턴 카운트가 80/100 임계값을 넘었을 때 `LastContext.SystemPrompt`에 힌트가 포함되는지, 100턴째에 `OnConversationEnded`가 발행되는지, 그 이후 `SendPlayerMessage`를 또 불러도 아무 일도 안 일어나는지 테스트 추가
- `KeywordIntroPanel`/`ChatBootstrap`의 UI 배선은 `ChatBubbleListView`와 마찬가지로 EditMode 테스트로 검증하기 어렵다 — Play 모드 수동 검증으로 확인한다

## 수동 검증 (End-to-End)

1. Play 모드 진입 → 채팅 UI는 안 보이고 키워드 선택 화면만 보이는지 확인
2. 키워드 2개만 고르면 "모험 시작" 버튼이 비활성 상태인지, 3개 고르면 활성화되는지 확인
3. 시작 버튼을 누르면 키워드 화면이 사라지고 채팅 UI가 나타나는지 확인
4. (턴 80/100까지 실제로 다 채우는 건 시간/비용이 크므로) `PlayerState.IncrementTurnCount`를 직접 여러 번 호출해서 턴 카운트를 인위적으로 80/100 근처로 만든 뒤, 다음 메시지를 보냈을 때 나레이션 톤이 마무리 쪽으로 가는지, 100턴째에 입력창/전송 버튼이 잠기는지 확인
