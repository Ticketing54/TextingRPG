using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace TextingRPG.Systems
{
    // 로컬 JSON 저장. 진행 중 게임 1개(current.json) + 완료작 히스토리(history/).
    // 파일 I/O 실패는 전부 삼키고 로그만 남긴다 — 저장 실패로 게임이 죽으면 안 된다.
    public static class SaveSystem
    {
        // 테스트에서 실제 세이브 폴더를 건드리지 않도록 루트를 바꿔치기할 수 있게 한다.
        internal static string RootOverride;

        private static string SaveDirectory =>
            RootOverride ?? Path.Combine(Application.persistentDataPath, "saves");
        private static string CurrentSavePath => Path.Combine(SaveDirectory, "current.json");
        private static string HistoryDirectory => Path.Combine(SaveDirectory, "history");
        private static string HistoryIndexPath => Path.Combine(HistoryDirectory, "index.json");

        public static void SaveCurrent(GameSaveData data)
        {
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(CurrentSavePath, JsonConvert.SerializeObject(data));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem.SaveCurrent 실패: {e.Message}");
            }
        }

        public static GameSaveData LoadCurrent() => ReadJson<GameSaveData>(CurrentSavePath);

        public static bool HasCurrent() => File.Exists(CurrentSavePath);

        // data.EndingTone이 채워진 상태로 넘겨받아 history/에 기록하고 current.json은 지운다.
        public static void ArchiveAsHistory(GameSaveData data)
        {
            try
            {
                Directory.CreateDirectory(HistoryDirectory);
                var id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
                File.WriteAllText(Path.Combine(HistoryDirectory, id + ".json"), JsonConvert.SerializeObject(data));

                var index = LoadHistoryIndex();
                index.Entries.Add(new HistoryEntry
                {
                    Id = id,
                    Title = string.IsNullOrEmpty(data.Outline?.Title) ? "(제목 없음)" : data.Outline.Title,
                    EndingTone = data.EndingTone,
                    SavedAtIso = data.SavedAtIso
                });
                File.WriteAllText(HistoryIndexPath, JsonConvert.SerializeObject(index));

                if (File.Exists(CurrentSavePath)) File.Delete(CurrentSavePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem.ArchiveAsHistory 실패: {e.Message}");
            }
        }

        public static HistoryIndex LoadHistoryIndex() => ReadJson<HistoryIndex>(HistoryIndexPath) ?? new HistoryIndex();

        public static GameSaveData LoadHistoryEntry(string id) =>
            ReadJson<GameSaveData>(Path.Combine(HistoryDirectory, id + ".json"));

        private static T ReadJson<T>(string path) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveSystem: {path} 읽기 실패: {e.Message}");
                return null;
            }
        }
    }
}
