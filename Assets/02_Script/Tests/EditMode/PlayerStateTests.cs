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

        [Test]
        public void GetSummary_DefaultsToEmptyString()
        {
            var state = new PlayerState();
            Assert.AreEqual("", state.GetSummary("npc_a"));
        }

        [Test]
        public void SetSummary_ThenGet_ReturnsSetValue()
        {
            var state = new PlayerState();
            state.SetSummary("npc_a", "플레이어가 여관에 도착했다.");
            Assert.AreEqual("플레이어가 여관에 도착했다.", state.GetSummary("npc_a"));
        }

        [Test]
        public void GetTurnCount_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetTurnCount("npc_a"));
        }

        [Test]
        public void IncrementTurnCount_IncreasesCountForThatNpc()
        {
            var state = new PlayerState();
            state.IncrementTurnCount("npc_a");
            state.IncrementTurnCount("npc_a");
            Assert.AreEqual(2, state.GetTurnCount("npc_a"));
        }

        [Test]
        public void IncrementTurnCount_DoesNotAffectOtherNpcsCount()
        {
            var state = new PlayerState();
            state.IncrementTurnCount("npc_a");
            Assert.AreEqual(0, state.GetTurnCount("npc_b"));
        }
    }
}
