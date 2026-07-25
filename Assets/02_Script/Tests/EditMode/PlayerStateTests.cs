using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class PlayerStateTests
    {
        [Test]
        public void GetRelationship_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetRelationship("npc_a"));
        }

        [Test]
        public void SetRelationship_ThenGet_ReturnsSetValue()
        {
            var state = new PlayerState();
            state.SetRelationship("npc_a", 5);
            Assert.AreEqual(5, state.GetRelationship("npc_a"));
        }

        [Test]
        public void GetHistory_ForUnknownNpc_ReturnsEmptyList()
        {
            var state = new PlayerState();
            var history = state.GetHistory("npc_a");
            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void AppendMessage_AddsToHistoryForThatNpc()
        {
            var state = new PlayerState();
            var message = new ChatMessage(ChatSender.Player, "안녕", "2026-07-25T00:00:00Z");

            state.AppendMessage("npc_a", message);

            var history = state.GetHistory("npc_a");
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("안녕", history[0].Text);
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
        }

        [Test]
        public void AppendMessage_DoesNotAffectOtherNpcsHistory()
        {
            var state = new PlayerState();
            state.AppendMessage("npc_a", new ChatMessage(ChatSender.Player, "hi a", "t"));

            Assert.AreEqual(0, state.GetHistory("npc_b").Count);
        }
    }
}
