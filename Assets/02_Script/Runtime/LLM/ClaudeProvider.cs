using System;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TextingRPG.Core;
using UnityEngine.Networking;

// BuildRequestBody is intentionally `internal` (see brief); the EditMode test assembly
// needs visibility into it to unit-test request-body construction without a network call.
[assembly: InternalsVisibleTo("TextingRPG.EditModeTests")]

namespace TextingRPG.LLM
{
    public class ClaudeProvider : ILLMProvider
    {
        // 사용 전 https://docs.anthropic.com/en/docs/about-claude/models 에서 최신 모델 ID를 확인할 것.
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string AnthropicVersion = "2023-06-01";
        private const int MaxTokens = 1024;

        private readonly string _apiKey;
        private readonly string _model;

        public ClaudeProvider(string apiKey, string model)
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
            var bodyJson = BuildRequestBody(context);
            var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

            var request = new UnityWebRequest(ApiUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("content-type", "application/json");
            request.SetRequestHeader("x-api-key", _apiKey);
            request.SetRequestHeader("anthropic-version", AnthropicVersion);

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
                    onError?.Invoke($"Failed to parse Claude response: {e.Message}");
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        internal string BuildRequestBody(ConversationContext context)
        {
            var messages = new JArray();
            foreach (var message in context.History)
            {
                messages.Add(new JObject
                {
                    ["role"] = message.Sender == ChatSender.Player ? "user" : "assistant",
                    ["content"] = message.Text
                });
            }

            var effectSchema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["type"] = new JObject { ["type"] = "string", ["enum"] = new JArray("relationship", "stat") },
                    ["target"] = new JObject { ["type"] = "string" },
                    ["delta"] = new JObject { ["type"] = "number" }
                },
                ["required"] = new JArray("type", "target", "delta")
            };

            var tool = new JObject
            {
                ["name"] = "npc_reply",
                ["description"] =
                    "Reply to the player in character, then report any relationship or stat effects this exchange caused.",
                ["input_schema"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["reply"] = new JObject { ["type"] = "string" },
                        ["effects"] = new JObject { ["type"] = "array", ["items"] = effectSchema }
                    },
                    ["required"] = new JArray("reply", "effects")
                }
            };

            var body = new JObject
            {
                ["model"] = _model,
                ["max_tokens"] = MaxTokens,
                ["system"] = context.SystemPrompt,
                ["messages"] = messages,
                ["tools"] = new JArray(tool),
                ["tool_choice"] = new JObject { ["type"] = "tool", ["name"] = "npc_reply" }
            };

            return body.ToString(Formatting.None);
        }

        public static LLMResponse ParseResponse(string rawJson)
        {
            var root = JObject.Parse(rawJson);
            var contentBlocks = (JArray)root["content"];

            if (contentBlocks != null)
            {
                foreach (var block in contentBlocks)
                {
                    if ((string)block["type"] != "tool_use")
                    {
                        continue;
                    }

                    var input = block["input"];
                    var response = new LLMResponse
                    {
                        Reply = (string)input["reply"],
                        Effects = new System.Collections.Generic.List<LLMEffect>()
                    };

                    var effectsToken = input["effects"] as JArray;
                    if (effectsToken != null)
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

            throw new Exception("No tool_use block found in Claude response.");
        }
    }
}
