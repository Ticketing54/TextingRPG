using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using UnityEngine.Networking;

namespace TextingRPG.LLM
{
    public class GeminiProvider : ILLMProvider
    {
        // 사용 전 https://ai.google.dev/gemini-api/docs/models 에서 최신 모델 ID를,
        // https://ai.google.dev/gemini-api/docs/structured-output 에서 responseSchema 형식을 확인할 것.
        private const string ApiUrlTemplate =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        private const int MaxOutputTokens = 2048; // thinking 모델은 응답 전에 추론 토큰을 소비하므로 여유 있게 잡는다

        private readonly string _apiKey;
        private readonly string _model;

        public GeminiProvider(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = model;
        }

        public void SendMessage(ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError)
        {
            SendMessageWithRetry(context, onSuccess, onError, retriesLeft: 1);
        }

        private void SendMessageWithRetry(
            ConversationContext context, Action<LLMResponse> onSuccess, Action<string> onError, int retriesLeft)
        {
            var url = string.Format(ApiUrlTemplate, _model);
            var bodyJson = BuildRequestBody(context);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityWebRequest(url, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("x-goog-api-key", _apiKey);

            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        if (retriesLeft > 0)
                        {
                            SendMessageWithRetry(context, onSuccess, onError, retriesLeft - 1);
                            return;
                        }

                        onError?.Invoke($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");
                        return;
                    }

                    var response = ParseResponse(request.downloadHandler.text);
                    onSuccess?.Invoke(response);
                }
                catch (Exception e)
                {
                    onError?.Invoke($"Failed to parse Gemini response: {e.Message}");
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        internal string BuildRequestBody(ConversationContext context)
        {
            var contents = new JArray();
            foreach (var message in context.History)
            {
                contents.Add(new JObject
                {
                    ["role"] = message.Sender == ChatSender.Player ? "user" : "model",
                    ["parts"] = new JArray(new JObject { ["text"] = message.Text })
                });
            }

            var effectSchema = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["type"] = new JObject { ["type"] = "STRING", ["enum"] = new JArray("relationship", "stat") },
                    ["target"] = new JObject { ["type"] = "STRING" },
                    ["delta"] = new JObject { ["type"] = "NUMBER" }
                },
                ["required"] = new JArray("type", "target", "delta")
            };

            var responseSchema = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["narration"] = new JObject { ["type"] = "STRING" },
                    ["npcLine"] = new JObject { ["type"] = "STRING" },
                    ["summary"] = new JObject { ["type"] = "STRING" },
                    ["effects"] = new JObject { ["type"] = "ARRAY", ["items"] = effectSchema }
                },
                ["required"] = new JArray("narration", "summary", "effects")
            };

            var body = new JObject
            {
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray(new JObject { ["text"] = context.SystemPrompt })
                },
                ["contents"] = contents,
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json",
                    ["responseSchema"] = responseSchema,
                    ["maxOutputTokens"] = MaxOutputTokens
                }
            };

            return body.ToString(Formatting.None);
        }

        public static LLMResponse ParseResponse(string rawJson)
        {
            var root = JObject.Parse(rawJson);
            var candidates = root["candidates"] as JArray;
            var parts = candidates != null && candidates.Count > 0
                ? candidates[0]["content"]?["parts"] as JArray
                : null;
            var text = parts != null && parts.Count > 0 ? (string)parts[0]["text"] : null;

            if (string.IsNullOrEmpty(text))
            {
                throw new Exception("No text part found in Gemini response.");
            }

            var payload = JObject.Parse(text);
            var response = new LLMResponse
            {
                Narration = (string)payload["narration"],
                NpcLine = (string)payload["npcLine"] ?? "",
                Summary = (string)payload["summary"] ?? "",
                Effects = new List<LLMEffect>()
            };

            if (payload["effects"] is JArray effectsToken)
            {
                foreach (var effectToken in effectsToken)
                {
                    response.Effects.Add(new LLMEffect
                    {
                        Type = (string)effectToken["type"],
                        Target = (string)effectToken["target"],
                        Delta = (float)effectToken["delta"]
                    });
                }
            }

            return response;
        }
    }
}
