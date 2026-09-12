using System;
using System.Collections.Generic;
using System.Linq;
using TextingRPG.Core;
using TextingRPG.LLM;
using TextingRPG.Systems;
using UnityEngine;

namespace TextingRPG.UI
{
    public class ChatController
    {
        private const int MaxHistoryMessages = 8;
        private const int WrapUpTurnThreshold = 100;
        private const int MaxTurns = 120;
        private const string OpeningKickoffMessage = "(모험이 시작된다.)";

        private readonly ILLMProvider _provider;
        private readonly string _worldDescription;
        private readonly string _playerName;

        private bool _conversationEnded;
        private bool _awaitingResponse;
        private List<Choice> _lastChoices = new List<Choice>();
        private PlayerEvaluation.Tally _tally; // 플레이 스타일 집계 (엔딩 평가용)

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        // 위험한 선택으로 주사위 두 개를 굴렸을 때 발생. (굴림 결과, 결과 등급 라벨, 애니메이션이
        // 끝나면 호출해야 하는 콜백)을 전달한다. 구독자가 없으면 애니메이션 없이 즉시 진행한다.
        public event Action<DiceRollResult, string, Action> OnDiceRollRequested;

        public ChatController(ILLMProvider provider, string worldDescription, string playerName)
        {
            _provider = provider;
            _worldDescription = worldDescription;
            _playerName = playerName;
        }

        public void BeginAdventure()
        {
            var systemPrompt = PromptBuilder.BuildStoryOutlinePrompt(_worldDescription, _playerName);
            // Gemini API는 contents가 빈 배열이면 요청을 거부하므로, 저장되지 않는 시작 트리거 턴 하나를 심어준다.
            var kickoff = new List<ChatMessage>
            {
                new ChatMessage(ChatSender.Player, OpeningKickoffMessage, DateTime.UtcNow.ToString("o"))
            };
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = kickoff };

            _provider.GenerateStoryOutline(
                context,
                onSuccess: (outline, openingNarration) =>
                {
                    DataManager.Instance.ApplyStoryOutline(outline);

                    var narrationMessage = new ChatMessage(ChatSender.Narration, openingNarration, DateTime.UtcNow.ToString("o"));
                    DataManager.Instance.AppendConversationMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    Debug.Log($"StoryOutline applied: {outline.Title}");
                },
                onError: error => OnError?.Invoke(error)
            );
        }

        public void SendPlayerMessage(string text)
        {
            if (_conversationEnded || _awaitingResponse) return;

            // 입력이 직전 턴 선택지의 번호면 그 선택지로 해석하고, 아니면 자유 입력으로 둔다.
            int? choiceIndex = ChoiceInputParser.Parse(text, _lastChoices.Count);
            Choice selectedChoice = choiceIndex.HasValue ? _lastChoices[choiceIndex.Value] : null;
            string playerText = selectedChoice != null ? selectedChoice.Text : text;

            var playerMessage = new ChatMessage(ChatSender.Player, playerText, DateTime.UtcNow.ToString("o"));
            DataManager.Instance.AppendConversationMessage(playerMessage);
            OnMessageAdded?.Invoke(playerMessage);

            if (selectedChoice != null) _tally.CountChoice(selectedChoice.Risk);

            _awaitingResponse = true;

            // 위험/무모 선택이면 2d6를 먼저 굴려두고, 애니메이션이 끝난 뒤에 LLM을 호출한다.
            // 안전한 선택/자유 입력은 굴릴 게 없으니 바로 진행한다.
            if (selectedChoice != null && selectedChoice.Risk != ChoiceRisk.Safe)
            {
                bool reckless = selectedChoice.Risk == ChoiceRisk.Reckless;
                var roll = DiceRoll.Roll();
                var outcome = DiceRoll.Bucket(roll.Total, reckless);
                _tally.CountOutcome(outcome);
                var directionHint = reckless
                    ? DiceDirectionText.ForReckless(outcome)
                    : DiceDirectionText.ForRisky(outcome);
                var outcomeLabel = $"{roll.A} + {roll.B} = {roll.Total} · {DiceDirectionText.ShortLabel(outcome)}";
                var diceResultLine = $"{DiceFaces.Glyph(roll.A)} {DiceFaces.Glyph(roll.B)}  {roll.Total} · {DiceDirectionText.ShortLabel(outcome)}";

                if (OnDiceRollRequested != null)
                    OnDiceRollRequested.Invoke(roll, outcomeLabel, () => ProceedWithTurn(selectedChoice, directionHint, diceResultLine));
                else
                    ProceedWithTurn(selectedChoice, directionHint, diceResultLine); // 오버레이 미구독(테스트 등) 시 즉시 진행
            }
            else
            {
                var directionHint = selectedChoice != null ? DiceDirectionText.ForSafe() : "";
                ProceedWithTurn(selectedChoice, directionHint);
            }
        }

        private void ProceedWithTurn(Choice selectedChoice, string directionHint, string diceResultLine = null)
        {
            // 주사위를 굴린 턴이면 결과를 별도의 가운데 정렬 나레이션 버블로 먼저 보여준다.
            // 화면 표시만 — 대화 히스토리/프롬프트에는 넣지 않는다 (LLM은 방향 문장만 본다).
            if (!string.IsNullOrEmpty(diceResultLine))
            {
                var diceMessage = new ChatMessage(ChatSender.Narration, diceResultLine, DateTime.UtcNow.ToString("o"));
                OnMessageAdded?.Invoke(diceMessage);
            }

            var fullHistory = DataManager.Instance.GetConversationHistory();
            int turnCount = fullHistory.Count(m => m.Sender == ChatSender.Player);
            bool isFinalTurn = turnCount >= MaxTurns;

            string endingHint = "";
            if (isFinalTurn)
                endingHint = "이번이 마지막 턴이다. 지금까지의 대화 흐름을 바탕으로 이야기를 자연스럽고 확실하게 마무리해라.";
            else if (turnCount >= WrapUpTurnThreshold)
                endingHint = "이야기가 슬슬 마무리를 향해 가야 한다. 남은 대화 안에서 자연스럽게 정리할 준비를 해라.";

            var hintParts = new List<string>();
            if (selectedChoice != null) hintParts.Add($"플레이어 선택: {selectedChoice.Text}");
            if (!string.IsNullOrEmpty(directionHint)) hintParts.Add(directionHint);
            if (!string.IsNullOrEmpty(endingHint)) hintParts.Add(endingHint);
            var extraHint = string.Join("\n", hintParts);

            var outline = DataManager.Instance.GetStoryOutline();
            var facts = DataManager.Instance.GetImportantFacts();
            var systemPrompt = PromptBuilder.BuildTurnPrompt(outline, facts, _playerName, extraHint);
            var recentHistory = HistoryWindow.TakeRecent(fullHistory, MaxHistoryMessages);
            var context = new ConversationContext { SystemPrompt = systemPrompt, History = recentHistory };

            _provider.ContinueStory(
                context,
                onSuccess: response =>
                {
                    _awaitingResponse = false;

                    if (response.IsOffTopic)
                    {
                        HandleOffTopic(response.Narration);
                        return;
                    }

                    var narrationMessage = new ChatMessage(ChatSender.Narration, response.Narration, DateTime.UtcNow.ToString("o"));
                    DataManager.Instance.AppendConversationMessage(narrationMessage);
                    OnMessageAdded?.Invoke(narrationMessage);

                    if (!string.IsNullOrEmpty(response.NpcLine))
                    {
                        var npcMessage = new ChatMessage(ChatSender.Npc, response.NpcLine, DateTime.UtcNow.ToString("o"), response.SpeakerName);
                        DataManager.Instance.AppendConversationMessage(npcMessage);
                        OnMessageAdded?.Invoke(npcMessage);
                    }

                    DataManager.Instance.AddImportantFacts(response.NewFacts);

                    _lastChoices = response.Choices ?? new List<Choice>();
                    bool ending = isFinalTurn || response.IsEnding;

                    if (!ending && _lastChoices.Count > 0)
                    {
                        var choicesMessage = new ChatMessage(
                            ChatSender.Narration, ChoiceListFormatter.Plain(_lastChoices), DateTime.UtcNow.ToString("o"))
                        {
                            DisplayText = ChoiceListFormatter.Colored(_lastChoices)
                        };
                        DataManager.Instance.AppendConversationMessage(choicesMessage);
                        OnMessageAdded?.Invoke(choicesMessage);
                    }

                    if (ending)
                    {
                        SaveSystem.ArchiveAsHistory(BuildSaveData(response.EndingTone));

                        _conversationEnded = true;
                        Debug.Log($"이야기 종료 — endingTone: '{response.EndingTone}'");

                        var evalMessage = new ChatMessage(
                            ChatSender.Narration, PlayerEvaluation.Summary(_tally), DateTime.UtcNow.ToString("o"));
                        OnMessageAdded?.Invoke(evalMessage);

                        OnConversationEnded?.Invoke();
                    }
                    else
                    {
                        SaveSystem.SaveCurrent(BuildSaveData(""));
                    }
                },
                onError: error =>
                {
                    _awaitingResponse = false;
                    OnError?.Invoke(error);
                }
            );
        }

        private GameSaveData BuildSaveData(string endingTone)
        {
            return new GameSaveData
            {
                PlayerName = _playerName,
                Outline = DataManager.Instance.GetStoryOutline(),
                ImportantFacts = DataManager.Instance.GetImportantFacts(),
                History = DataManager.Instance.GetConversationHistory(),
                LastChoices = _lastChoices,
                Tally = _tally,
                EndingTone = endingTone ?? "",
                SavedAtIso = DateTime.UtcNow.ToString("o")
            };
        }

        // 플레이어 입력이 이야기와 무관할 때: 그 입력을 턴/히스토리에서 빼고, 짧은 안내와
        // 직전 선택지를 다시 보여준다. 화면의 플레이어 버블은 그대로 남는다.
        private void HandleOffTopic(string redirectLine)
        {
            var history = DataManager.Instance.GetConversationHistory();
            if (history.Count > 0 && history[history.Count - 1].Sender == ChatSender.Player)
                history.RemoveAt(history.Count - 1);

            var line = string.IsNullOrWhiteSpace(redirectLine)
                ? "(지금 이야기와 관련 없는 말 같다. 하던 데서 이어가자.)"
                : redirectLine;
            OnMessageAdded?.Invoke(new ChatMessage(ChatSender.Narration, line, DateTime.UtcNow.ToString("o")));

            if (_lastChoices.Count > 0)
            {
                OnMessageAdded?.Invoke(new ChatMessage(
                    ChatSender.Narration, ChoiceListFormatter.Plain(_lastChoices), DateTime.UtcNow.ToString("o"))
                {
                    DisplayText = ChoiceListFormatter.Colored(_lastChoices)
                });
            }
        }
    }
}
