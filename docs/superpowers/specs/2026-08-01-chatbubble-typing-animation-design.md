# ChatBubble — 타자기 애니메이션 & 상태머신 설계

## 개요

채팅 리스트에 표시되는 말풍선 하나(`ChatBubble`)가, 자신에게 주어진 메시지를 어떻게 "재생"하는지에 대한 설계다. 메시지를 실제로 생성/배치/스크롤하는 리스트 컨트롤러는 이번 스펙 범위 밖이며, `ChatBubble`은 프리팹 하나에 붙는 자기완결적 컴포넌트로서 "메시지 하나를 받아 스스로 애니메이션 재생"하는 역할만 가진다.

핵심 요구사항:
- 발신자(`Player`/`Npc`/`Narration`)에 따라 말풍선을 좌/우/가운데로 정렬
- 본인(`Player`)이 보낸 메시지는 즉시 전체 텍스트 표시, 그 외(`Npc`, `Narration`)는 타자기처럼 한 글자씩 순차 표시
- 페이드/스케일 등 등장 애니메이션은 다루지 않는다 (스케일은 이미 `ContentSizeFitter`가 처리)
- 말풍선의 진행 상태를 상태머신으로 명시적으로 기록
- 타이핑 도중 스킵(즉시 완성) 가능해야 하지만, 스킵을 트리거하는 입력 방식은 이번 스펙 범위 밖

## 데이터 모델 변경

`ChatMessage.cs`의 `ChatSender`에 `Narration`을 추가한다.

```csharp
public enum ChatSender { Player, Npc, Narration }
```

기존 코드에서 `ChatSender`를 소비하는 곳은 `GeminiProvider.cs`의 삼항연산자(`Player`→"user", 그 외→"model") 하나뿐이라, 값 추가만으로는 기존 동작을 깨뜨리지 않는다. 나레이션 메시지를 LLM 히스토리에 어떻게 반영할지, `ChatController`/`StoryNode`가 나레이션 메시지를 언제 생성할지는 이번 스펙 범위 밖이며 추후 별도로 다룬다.

## 컴포넌트 구조 & 공개 API

```csharp
public enum ChatBubbleState { Idle, Typing, Completed }

public class ChatBubble : MonoBehaviour
{
    [SerializeField] VerticalLayoutGroup verticalLayoutGroup;
    [SerializeField] RectTransform rectTransform_Bubble;
    [SerializeField] TextMeshProUGUI textMeshProUGUI_text;
    [SerializeField] float charsPerSecond = 40f;

    public ChatBubbleState State { get; private set; } = ChatBubbleState.Idle;
    public event Action OnPlayComplete;

    public void Play(ChatSender sender, string text);
    public void Skip();
}
```

- `Play(sender, text)`가 유일한 진입점이다. 나중에 만들 리스트 컨트롤러는 버블을 인스턴스화한 뒤 이 메서드 한 번만 호출하면 된다.
- `Skip()`은 public이며, 스킵을 트리거하는 입력(탭/키 입력 등)은 이번 스펙에서 구현하지 않는다. 추후 다른 입력 트리거에서 이 메서드를 직접 호출하는 것을 전제로 열어둔다.
- `OnPlayComplete`는 애니메이션(타이핑 포함)이 끝났을 때 호출되어, 리스트 컨트롤러가 스크롤 등 후속 동작을 걸 수 있게 하는 훅이다.
- `verticalLayoutGroup`은 버블 자체가 아니라, 버블이 속한 행(row)의 `childAlignment`를 조정해 좌/우/가운데 정렬을 구현하는 데 쓰인다 (기존 프리팹 구조 유지).

### 정렬 매핑

| ChatSender | childAlignment |
|---|---|
| `Player` | `UpperRight` |
| `Npc` | `UpperLeft` |
| `Narration` | `UpperCenter` |

## 상태머신

```
Idle --Play()--> Typing --(전체 글자 노출 완료 / Skip())--> Completed
Idle --Play(sender: Player)--> Completed   // 즉시 완료, Typing을 거치지 않음
```

- `Idle`: `Play()` 호출 전 초기 상태
- `Typing`: 타자기 리빌이 진행 중. `Player` 메시지는 이 상태를 거치지 않는다
- `Completed`: 전체 텍스트가 화면에 표시된 최종 상태 (타이핑 정상 종료 또는 `Skip()`으로 도달)

`State`는 외부에서 읽기 전용으로 노출되어, 향후 리스트 컨트롤러나 입력 시스템이 "지금 타이핑 중인 말풍선이 있는지"를 판단하는 데 사용할 수 있다.

## 타자기 리빌 방식

전체 텍스트 길이만큼 처음부터 공백으로 채운 문자열을 세팅해 둔 뒤, 앞에서부터 한 글자씩 실제 문자로 교체하는 방식을 쓴다. 이렇게 하면 텍스트 길이가 애니메이션 내내 일정하게 유지되어, `ContentSizeFitter`가 처음부터 최종 크기로 버블을 잡고 타이핑 도중 버블이 커지거나 흔들리지 않는다.

```csharp
private Tween _typingTween;
private string _fullText;

public void Play(ChatSender sender, string text)
{
    verticalLayoutGroup.childAlignment = AlignmentFor(sender);
    _typingTween?.Kill();

    if (sender == ChatSender.Player)
    {
        textMeshProUGUI_text.text = text;
        SetState(ChatBubbleState.Completed);
        return;
    }

    StartTyping(text);
}

public void Skip()
{
    if (State != ChatBubbleState.Typing) return;
    _typingTween?.Kill();
    textMeshProUGUI_text.text = _fullText;
    SetState(ChatBubbleState.Completed);
}

private void StartTyping(string text)
{
    _fullText = text;
    SetState(ChatBubbleState.Typing);
    textMeshProUGUI_text.text = new string(' ', text.Length);

    int revealed = 0;
    float duration = Mathf.Max(0.01f, text.Length / charsPerSecond);
    _typingTween = DOTween.To(() => revealed, v => revealed = v, text.Length, duration)
        .SetEase(Ease.Linear)
        .OnUpdate(() => textMeshProUGUI_text.text = text.Substring(0, revealed) + new string(' ', text.Length - revealed))
        .OnComplete(() => SetState(ChatBubbleState.Completed));
}

private void SetState(ChatBubbleState state)
{
    State = state;
    if (state == ChatBubbleState.Completed) OnPlayComplete?.Invoke();
}

private static TextAnchor AlignmentFor(ChatSender sender) => sender switch
{
    ChatSender.Player => TextAnchor.UpperRight,
    ChatSender.Narration => TextAnchor.UpperCenter,
    _ => TextAnchor.UpperLeft
};
```

- 빈 문자열(`text.Length == 0`)일 때 0으로 나누는 것을 막기 위해 `duration`에 최소값(`0.01f`)을 clamp한다.
- `Play()`가 재생 중에 다시 호출되면(버블 재사용/풀링 대비) 기존 `_typingTween`을 `Kill()`하고 새로 시작한다.
- DOTween의 정수 트윈(`DOTween.To(() => int, v => ..., endValue, duration)`)은 내장 지원되는 타입이라 별도 플러그인이 필요 없다.

## 테스트 / 검증 전략

`ChatBubble`은 DOTween 트윈과 `MonoBehaviour` 생명주기에 의존하므로 EditMode 유닛 테스트로 검증하기 어렵다. 대신:

- 테스트 씬에 프리팹을 배치하고 Play 모드에서 `Play(sender, text)`를 각 `ChatSender` 값(`Player`/`Npc`/`Narration`)에 대해 직접 호출해, 정렬과 타이핑 리빌이 의도대로 동작하는지 육안으로 확인한다
- `Skip()`을 타이핑 도중/`Completed` 상태/`Idle` 상태 각각에서 호출해 상태별로 올바르게 무시되거나 즉시 완료되는지 확인한다
- 자동화된 테스트는 두지 않는다 (범위 밖)

## 범위 밖 (이번 스펙에서 다루지 않음)

- `ChatController.OnMessageAdded`를 구독해 버블을 실제로 생성/배치/스크롤하는 리스트 컨트롤러
- 등장 페이드/스케일 애니메이션 (스케일은 `ContentSizeFitter`가 이미 처리)
- 스킵을 트리거하는 입력 방식(탭, 클릭, 키 입력 등) — `Skip()`은 public API로만 제공
- 나레이션 메시지를 실제로 생성/발송하는 로직 (`ChatController`, `StoryNode` 등에서 나레이션을 언제/어떻게 만들지)
- 버블 프리팹의 시각적 스타일(색상, 여백 등)
