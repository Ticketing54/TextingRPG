using System;
using System.IO;
using UnityEngine;

namespace TextingRPG.Core
{
    // 커밋되지 않는 프로젝트 루트의 secrets.local.json에서 로컬 개발용 API 키를 읽는다.
    public static class LocalSecrets
    {
        private const string FileName = "secrets.local.json";

        public static string GetGeminiApiKey()
        {
            var path = Path.Combine(Application.dataPath, "..", FileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"{FileName}이(가) 프로젝트 루트에 없습니다. geminiApiKey 값을 채운 뒤 다시 시도하세요.");
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SecretsData>(json);
                return string.IsNullOrEmpty(data?.geminiApiKey) ? null : data.geminiApiKey;
            }
            catch (Exception e)
            {
                Debug.LogError($"{FileName} 파싱 실패: {e.Message}");
                return null;
            }
        }

        [Serializable]
        private class SecretsData
        {
            public string geminiApiKey;
        }
    }
}
