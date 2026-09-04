using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using TextingRPG.Systems;
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

        public void GenerateStoryOutline(
            ConversationContext context, Action<DataManager.StoryOutline, string> onSuccess, Action<string> onError)
        {
            var url = string.Format(ApiUrlTemplate, _model);
            var bodyJson = BuildStoryOutlineRequestBody(context);
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
                        onError?.Invoke($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");
                        return;
                    }

                    var (outline, openingNarration) = ParseStoryOutline(request.downloadHandler.text);
                    onSuccess?.Invoke(outline, openingNarration);
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

        internal string BuildStoryOutlineRequestBody(ConversationContext context)
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

            var stringArraySchema = new JObject { ["type"] = "ARRAY", ["items"] = new JObject { ["type"] = "STRING" } };

            var responseSchema = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["title"] = new JObject { ["type"] = "STRING" },
                    ["worldSetting"] = new JObject { ["type"] = "STRING" },
                    ["keyCharacters"] = stringArraySchema,
                    ["keyEvents"] = stringArraySchema,
                    ["finalGoal"] = new JObject { ["type"] = "STRING" },
                    ["openingNarration"] = new JObject { ["type"] = "STRING" }
                },
                ["required"] = new JArray("title", "worldSetting", "keyCharacters", "keyEvents", "finalGoal", "openingNarration")
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

        public static (DataManager.StoryOutline Outline, string OpeningNarration) ParseStoryOutline(string rawJson)
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
            var outline = new DataManager.StoryOutline
            {
                Title = (string)payload["title"],
                WorldSetting = (string)payload["worldSetting"],
                FinalGoal = (string)payload["finalGoal"],
                KeyCharacters = new List<string>(),
                KeyEvents = new List<string>()
            };

            if (payload["keyCharacters"] is JArray charactersToken)
            {
                foreach (var character in charactersToken) outline.KeyCharacters.Add((string)character);
            }

            if (payload["keyEvents"] is JArray eventsToken)
            {
                foreach (var evt in eventsToken) outline.KeyEvents.Add((string)evt);
            }

            var openingNarration = (string)payload["openingNarration"] ?? "";

            return (outline, openingNarration);
        }

        public void ContinueStory(ConversationContext context, Action<TurnResponse> onSuccess, Action<string> onError)
        {
            var url = string.Format(ApiUrlTemplate, _model);
            var bodyJson = BuildTurnRequestBody(context);
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
                        onError?.Invoke($"{request.responseCode}: {request.error} — {request.downloadHandler.text}");
                        return;
                    }

                    var response = ParseTurnResponse(request.downloadHandler.text);
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

        internal string BuildTurnRequestBody(ConversationContext context)
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

            var responseSchema = new JObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JObject
                {
                    ["narration"] = new JObject { ["type"] = "STRING" },
                    ["npcLine"] = new JObject { ["type"] = "STRING" },
                    ["speakerName"] = new JObject { ["type"] = "STRING" },
                    ["newFacts"] = new JObject { ["type"] = "ARRAY", ["items"] = new JObject { ["type"] = "STRING" } },
                    ["isEnding"] = new JObject { ["type"] = "BOOLEAN" }
                },
                ["required"] = new JArray("narration", "newFacts", "isEnding")
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

        public static TurnResponse ParseTurnResponse(string rawJson)
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
            var response = new TurnResponse
            {
                Narration = (string)payload["narration"],
                NpcLine = (string)payload["npcLine"] ?? "",
                SpeakerName = (string)payload["speakerName"] ?? "",
                IsEnding = (bool?)payload["isEnding"] ?? false,
                NewFacts = new List<string>()
            };

            if (payload["newFacts"] is JArray factsToken)
            {
                foreach (var fact in factsToken) response.NewFacts.Add((string)fact);
            }

            return response;
        }
    }
}
