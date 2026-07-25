using System.Collections.Generic;
using NUnit.Framework;
using TextingRPG.LLM;

namespace TextingRPG.Tests
{
    public class MockLLMProviderTests
    {
        [Test]
        public void SendMessage_InvokesOnSuccessWithNextResponse()
        {
            var provider = new MockLLMProvider
            {
                NextResponse = new LLMResponse { Reply = "안녕!", Effects = new List<LLMEffect>() }
            };
            var context = new ConversationContext { SystemPrompt = "test", History = new List<Core.ChatMessage>() };

            LLMResponse received = null;
            provider.SendMessage(context, r => received = r, e => Assert.Fail("onError should not be called"));

            Assert.AreEqual("안녕!", received.Reply);
        }

        [Test]
        public void SendMessage_RecordsLastContext()
        {
            var provider = new MockLLMProvider { NextResponse = new LLMResponse { Reply = "x", Effects = new List<LLMEffect>() } };
            var context = new ConversationContext { SystemPrompt = "system-prompt-xyz", History = new List<Core.ChatMessage>() };

            provider.SendMessage(context, _ => { }, _ => { });

            Assert.AreEqual("system-prompt-xyz", provider.LastContext.SystemPrompt);
        }

        [Test]
        public void SendMessage_WithNextErrorSet_InvokesOnErrorInstead()
        {
            var provider = new MockLLMProvider { NextError = "network down" };
            var context = new ConversationContext { SystemPrompt = "s", History = new List<Core.ChatMessage>() };

            string receivedError = null;
            provider.SendMessage(context, _ => Assert.Fail("onSuccess should not be called"), e => receivedError = e);

            Assert.AreEqual("network down", receivedError);
        }
    }
}
