# ChatBubbleListView — 메시지 큐 재생 & 리스트 렌더링 설계

## 개요

`ChatController.OnMessageAdded`로 들어오는 메시지들을 실제로 `ChatBubble` 프리팹 인스턴스로 만들어 화면에 순서대로 보여주는 리스트 뷰(`ChatBubbleListView`)를 설계한다. `ChatBubble`(프리팹 하나에 붙는 자기완결적 컴포넌트, `2026-08-01-chatbubble-typing-animation-design.md` 참고)이 "메시지 하나를 재생"하는 역할이었다면, 이번 컴포넌트는 "메시지들을 순서대로 재생시키는" 오케스트레이션 역할이다.

핵심 요구사항:
- `ChatController`가 발생시키는 메시지들을 큐에 쌓고, 한 번에 하나의 버블만 타이핑되도록 순차 재생한다
- 새 버블이 생성될 때마다 스크롤을 맨 아래로 이동시킨다
- 버블을 실제로 생성/배치할 부모(`Content`)는 씬에 이미 구성된 `Scroll View > Viewport > Content` 구조를 그대로 사용한다

범위 밖 (이번 스펙에서 다루지 않음):
- `ChatController`를 실제로 생성해서 주입하는 게임 부트스트랩 (`PlayerState`/`ILLMProvider`/`NPCDefinition`/`StoryGraph` 조립) — `ChatController`는 이미 만들어져 있다고 가정하고, 외부에서 `Bind(ChatController)`를 호출해주는 것을 전제로 한다
- "이미 맨 아래일 때만 자동 스크롤" 같은 스마트 스크롤 — 항상 무조건 맨 아래로 스크롤한다
- 버블 재사용/오브젝트 풀링 — 메시지마다 새로 `Instantiate`한다
- 스킵 트리거 입력 (탭/클릭 등으로 현재 타이핑 중인 버블을 `Skip()`시키는 것) — `ChatBubble.Skip()`은 이미 public이지만, 이걸 호출하는 입력 처리는 범위 밖

## 컴포넌트 구조 & 공개 API

```csharp
public class ChatBubbleListView : MonoBehaviour
{
    [SerializeField] ChatBubble chatBubblePrefab;
    [SerializeField] RectTransform content;
    [SerializeField] ScrollRect scrollRect;

    public void Bind(ChatController controller);
}
```

- `Bind(controller)`가 유일한 진입점이다. 나중에 만들 부트스트랩이 `ChatController`를 생성한 뒤 이 메서드를 한 번 호출하면 된다.
- `ChatController`는 순수 C# 클래스라 `[SerializeField]`로 인스펙터에 노출할 수 없다. 그래서 생성자 주입이 아니라 `Bind` 메서드 주입 방식을 쓴다.
- `chatBubblePrefab`은 `ChatBubble` 컴포넌트가 붙은 프리팹(`Assets/ChatBubble.prefab`)을 가리킨다.
- `content`는 씬의 `Scroll View > Viewport > Content`의 `RectTransform`, `scrollRect`는 같은 `Scroll View`의 `ScrollRect` 컴포넌트를 가리킨다.

## 동작 흐름 (메시지 큐 재생)

```csharp
private readonly Queue<ChatMessage> _pending = new Queue<ChatMessage>();
private ChatBubble _current;
private ChatController _controller;

public void Bind(ChatController controller)
{
    if (_controller != null) _controller.OnMessageAdded -= Enqueue;
    _controller = controller;
    _controller.OnMessageAdded += Enqueue;
}

private void OnDestroy()
{
    if (_controller != null) _controller.OnMessageAdded -= Enqueue;
}

private void Enqueue(ChatMessage message)
{
    _pending.Enqueue(message);
    TryPlayNext();
}

private void TryPlayNext()
{
    if (_current != null) return;
    if (_pending.Count == 0) return;

    var message = _pending.Dequeue();
    _current = Instantiate(chatBubblePrefab, content);
    _current.OnPlayComplete += HandleCurrentComplete;
    _current.Play(message.Sender, message.Text);

    Canvas.ForceUpdateCanvases();
    scrollRect.verticalNormalizedPosition = 0f;
}

private void HandleCurrentComplete()
{
    _current.OnPlayComplete -= HandleCurrentComplete;
    _current = null;
    TryPlayNext();
}
```

- `Enqueue`는 메시지를 큐에 넣고 즉시 `TryPlayNext()`를 시도한다 — 재생 중인 버블(`_current`)이 없을 때만 실제로 다음 메시지를 꺼내 재생한다.
- `Player` 발신 메시지는 `ChatBubble.Play()`가 동기적으로 즉시 `Completed` 상태가 되고 `OnPlayComplete`를 그 자리에서 호출하므로(`ChatBubble` 스펙 참고), `HandleCurrentComplete`가 같은 호출 스택 안에서 재귀적으로 실행되며 큐에 남은 다음 메시지가 바로 이어서 재생된다. 별도 처리가 필요 없다.
- `Npc`/`Narration` 발신 메시지는 DOTween 타이핑이 끝나야 `OnPlayComplete`가 호출되므로, 그 사이에 들어오는 메시지는 큐에 쌓여있다가 순서대로 재생된다.
- `Bind`를 두 번 호출하는 경우(재사용 대비) 기존 구독을 해제하고 새로 구독한다.
- 스크롤은 버블을 생성한 직후 `Canvas.ForceUpdateCanvases()`로 레이아웃을 강제 갱신한 뒤 `scrollRect.verticalNormalizedPosition = 0f`로 맨 아래로 이동시킨다. `ChatBubble`은 타이핑 시작 시점에 이미 최종 텍스트 길이만큼 공백으로 채워두므로(스페이스 패딩 방식) 버블 행의 높이가 타이핑 도중 바뀌지 않고, 생성 시점 스크롤 한 번으로 충분하다.

## 테스트 / 검증 전략

`ChatBubbleListView`는 `MonoBehaviour`, `Instantiate`, `ScrollRect`, 그리고 `ChatBubble`(DOTween 의존)에 의존하므로 `ChatBubble`과 마찬가지로 EditMode 유닛 테스트로 검증하기 어렵다. 대신:

- 테스트 씬에서 `Bind()`한 뒤 `ChatController` 대신 더미로 여러 메시지를 연속으로 `OnMessageAdded`를 통해 흘려보내, 버블들이 겹치지 않고 순서대로 하나씩 타이핑되는지 육안으로 확인한다
- 새 버블이 생길 때마다 스크롤이 맨 아래로 이동하는지 확인한다
- `Player` 메시지 다음에 곧바로 `Npc` 메시지가 큐에 들어와도(동기 완료 → 재귀 재생) 순서가 꼬이지 않는지 확인한다
- 자동화된 테스트는 두지 않는다 (범위 밖)

## 범위 밖 (재확인)

- `ChatController`를 실제로 조립하는 게임 부트스트랩
- 스마트 스크롤(사용자가 위로 스크롤해서 과거 글을 읽는 중이면 자동 스크롤 안 하기)
- 버블 오브젝트 풀링/재사용
- 타이핑 스킵을 트리거하는 입력 처리
