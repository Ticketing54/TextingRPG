using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.Core;

namespace TextingRPG.Tests
{
    public class HistoryWindowTests
    {
        private static List<ChatMessage> MakeMessages(int count)
        {
            var messages = new List<ChatMessage>();
            for (int i = 0; i < count; i++)
            {
                messages.Add(new ChatMessage(ChatSender.Player, $"msg{i}", $"t{i}"));
            }
            return messages;
        }

        [Test]
        public void TakeRecent_HistoryShorterThanMax_ReturnsFullHistory()
        {
            var history = MakeMessages(3);

            var result = HistoryWindow.TakeRecent(history, 8);

            Assert.AreEqual(3, result.Count);
        }

        [Test]
        public void TakeRecent_HistoryLongerThanMax_ReturnsOnlyLastNMessages()
        {
            var history = MakeMessages(10);

            var result = HistoryWindow.TakeRecent(history, 4);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("msg6", result[0].Text);
            Assert.AreEqual("msg9", result[3].Text);
        }

        [Test]
        public void TakeRecent_PreservesOriginalOrder()
        {
            var history = MakeMessages(5);

            var result = HistoryWindow.TakeRecent(history, 3);

            CollectionAssert.AreEqual(new[] { "msg2", "msg3", "msg4" },
                result.ConvertAll(m => m.Text));
        }
    }
}
