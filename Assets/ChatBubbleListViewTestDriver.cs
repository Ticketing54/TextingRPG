using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using TextingRPG.Story;
using TextingRPG.UI;
using UnityEngine;

public class ChatBubbleListViewTestDriver : MonoBehaviour
{
    [SerializeField] ChatBubbleListView chatBubbleListView;

    private static readonly (string Player, string Npc)[] Turns =
    {
        ("안녕하세요!", "어서오세요, 손님! 오늘은 무엇을 도와드릴까요?"),
        ("이 근처에 여관이 있나요?", "네, 광장 건너편에 있습니다. 방을 잡아드릴까요?"),
        ("네, 부탁드려요.", "알겠습니다, 열쇠 여기 있습니다. 좋은 밤 되세요!"),
        ("혹시 근처에 대장간도 있나요?", "대장간은 여관 옆 골목으로 들어가시면 바로 보이실 거예요."),
        ("감사합니다!", "별말씀을요, 또 오세요!")
    };

    private ChatController _controller;
    private MockLLMProvider _provider;
    private int _nextTurnIndex;

    private void Start()
    {
        var npc = ScriptableObject.CreateInstance<NPCDefinition>();
        npc.NpcId = "test_npc";
        npc.DisplayName = "테스트 NPC";
        npc.PersonaDescription = "친절한 테스트용 상인";

        var storyGraph = ScriptableObject.CreateInstance<StoryGraph>();
        storyGraph.NpcId = "test_npc";
        storyGraph.StartNodeId = "start";
        storyGraph.Nodes = new()
        {
            new StoryNode { NodeId = "start", SceneDescription = "가게 안, 손님이 막 들어왔다." }
        };

        _provider = new MockLLMProvider();
        _controller = new ChatController(new PlayerState(), _provider, npc, "테스트 세계관 설명", storyGraph);

        chatBubbleListView.Bind(_controller);
    }

    // Hook this up to a UI Button's OnClick in the Inspector.
    public void SendNextTurn()
    {
        var turn = Turns[_nextTurnIndex % Turns.Length];
        _nextTurnIndex++;

        _provider.NextResponse = new LLMResponse { Reply = turn.Npc };
        _controller.SendPlayerMessage(turn.Player);
    }
}
