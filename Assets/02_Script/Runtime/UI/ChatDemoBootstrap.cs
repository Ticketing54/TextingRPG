using System;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.NPC;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private ChatUIView _chatUIView;
        [SerializeField] private NPCDefinition _npc;
        [SerializeField] private string _worldDescription = "이곳은 중세 판타지 마을이다.";
        [SerializeField] private string _claudeModel = "claude-sonnet-4-5-20250929";

        private void Start()
        {
            if (_npc == null)
            {
                Debug.LogError("ChatDemoBootstrap: _npc가 할당되어 있지 않습니다.");
                if (_chatUIView != null)
                {
                    _chatUIView.ShowError("NPC 설정이 누락되었습니다.");
                }
                return;
            }

            if (_chatUIView == null)
            {
                Debug.LogError("ChatDemoBootstrap: _chatUIView가 할당되어 있지 않습니다.");
                return;
            }

            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("ANTHROPIC_API_KEY 환경변수가 설정되어 있지 않습니다.");
                _chatUIView.ShowError("오류: ANTHROPIC_API_KEY 환경변수가 설정되어 있지 않습니다.");
                return;
            }

            var playerState = new PlayerState();
            ILLMProvider provider = new ClaudeProvider(apiKey, _claudeModel);
            var systemPrompt = SystemPromptBuilder.Build(_npc, _worldDescription);
            var controller = new ChatController(playerState, provider, _npc.NpcId, systemPrompt);

            _chatUIView.Init(controller);
        }
    }
}
