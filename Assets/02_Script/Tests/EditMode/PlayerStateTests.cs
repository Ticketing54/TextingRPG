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
        public void GetHistory_Initially_ReturnsEmptyList()
        {
            var state = new PlayerState();
            var history = state.GetHistory();
            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void AppendMessage_AddsToHistory()
        {
            var state = new PlayerState();
            var message = new ChatMessage(ChatSender.Player, "안녕", "2026-07-25T00:00:00Z");

            state.AppendMessage(message);

            var history = state.GetHistory();
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("안녕", history[0].Text);
            Assert.AreEqual(ChatSender.Player, history[0].Sender);
        }

        [Test]
        public void GetSummary_DefaultsToEmptyString()
        {
            var state = new PlayerState();
            Assert.AreEqual("", state.GetSummary());
        }

        [Test]
        public void SetSummary_ThenGet_ReturnsSetValue()
        {
            var state = new PlayerState();
            state.SetSummary("플레이어가 여관에 도착했다.");
            Assert.AreEqual("플레이어가 여관에 도착했다.", state.GetSummary());
        }

        [Test]
        public void GetTurnCount_DefaultsToZero()
        {
            var state = new PlayerState();
            Assert.AreEqual(0, state.GetTurnCount());
        }

        [Test]
        public void IncrementTurnCount_IncreasesCount()
        {
            var state = new PlayerState();
            state.IncrementTurnCount();
            state.IncrementTurnCount();
            Assert.AreEqual(2, state.GetTurnCount());
        }
    }
}
