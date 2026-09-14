using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.Systems;

namespace TextingRPG.Tests
{
    // 실제 세이브 폴더(persistentDataPath)를 건드리지 않도록 매 테스트마다 임시 폴더로 바꿔치기한다.
    public class SaveSystemTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "TextingRPG_SaveSystemTests_" + Guid.NewGuid().ToString("N"));
            SaveSystem.RootOverride = _tempRoot;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.RootOverride = null;
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        }

        private static GameSaveData SampleData(string endingTone = "") => new GameSaveData
        {
            PlayerName = "카린",
            Outline = new DataManager.StoryOutline { Title = "별빛을 찾는 항해" },
            ImportantFacts = new List<string> { "왕이 죽었다" },
            History = new List<ChatMessage> { new ChatMessage(ChatSender.Narration, "이야기가 시작된다.", "t") },
            LastChoices = new List<Choice> { new Choice("문을 연다", ChoiceRisk.Risky) },
            EndingTone = endingTone,
            SavedAtIso = DateTime.UtcNow.ToString("o")
        };

        [Test]
        public void HasCurrent_FalseUntilSaved()
        {
            Assert.IsFalse(SaveSystem.HasCurrent());
            SaveSystem.SaveCurrent(SampleData());
            Assert.IsTrue(SaveSystem.HasCurrent());
        }

        [Test]
        public void SaveCurrent_ThenLoadCurrent_RoundTrips()
        {
            SaveSystem.SaveCurrent(SampleData());

            var loaded = SaveSystem.LoadCurrent();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("카린", loaded.PlayerName);
            Assert.AreEqual("별빛을 찾는 항해", loaded.Outline.Title);
            Assert.AreEqual(1, loaded.History.Count);
            Assert.AreEqual("이야기가 시작된다.", loaded.History[0].Text);
            Assert.AreEqual("문을 연다", loaded.LastChoices[0].Text);
        }

        [Test]
        public void LoadCurrent_NoFile_ReturnsNull()
        {
            Assert.IsNull(SaveSystem.LoadCurrent());
        }

        [Test]
        public void ArchiveAsHistory_MovesToHistoryAndClearsCurrent()
        {
            SaveSystem.SaveCurrent(SampleData());

            SaveSystem.ArchiveAsHistory(SampleData("good"));

            Assert.IsFalse(SaveSystem.HasCurrent());

            var index = SaveSystem.LoadHistoryIndex();
            Assert.AreEqual(1, index.Entries.Count);
            Assert.AreEqual("별빛을 찾는 항해", index.Entries[0].Title);
            Assert.AreEqual("good", index.Entries[0].EndingTone);

            var entry = SaveSystem.LoadHistoryEntry(index.Entries[0].Id);
            Assert.IsNotNull(entry);
            Assert.AreEqual("good", entry.EndingTone);
            Assert.AreEqual("이야기가 시작된다.", entry.History[0].Text);
        }

        [Test]
        public void ArchiveAsHistory_Twice_AppendsBothToIndex()
        {
            SaveSystem.ArchiveAsHistory(SampleData("good"));
            SaveSystem.ArchiveAsHistory(SampleData("bad"));

            var entries = SaveSystem.LoadHistoryIndex().Entries;

            Assert.AreEqual(2, entries.Count);
        }

        [Test]
        public void LoadHistoryIndex_NoHistory_ReturnsEmpty()
        {
            var index = SaveSystem.LoadHistoryIndex();

            Assert.IsNotNull(index);
            Assert.AreEqual(0, index.Entries.Count);
        }
    }
}
