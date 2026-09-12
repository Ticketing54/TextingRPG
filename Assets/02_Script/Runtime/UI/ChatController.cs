using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        // 선택지 태그 라벨 색 (TMP 리치텍스트 hex). 배경 보고 조정하기 쉽게 여기 모아둔다.
        private const string SafeChoiceColor = "#2E9E5B";     // 초록
        private const string RiskyChoiceColor = "#C88A1E";    // 진노랑/골드 — 순수 노랑은 밝은 배경에서 안 보임
        private const string RecklessChoiceColor = "#D33A3A"; // 빨강

        private readonly ILLMProvider _provider;
        private readonly string _worldDescription;
        private readonly string _playerName;

        private bool _conversationEnded;
        private bool _awaitingResponse;
        private List<Choice> _lastChoices = new List<Choice>();
        private Tally _tally; // 플레이 스타일 집계 (엔딩 평가용)

        public event Action<ChatMessage> OnMessageAdded;
        public event Action<string> OnError;
        public event Action OnConversationEnded;

        // 위험한 선택으로 주사위 두 개를 굴렸을 때 발생. (굴림 결과, 결과 등급 라벨, 애니메이션이
        // 끝나면 호출해야 하는 콜백)을 전달한다. 구독자가 없으면 애니메이션 없이 즉시 진행한다.
        public event Action<DiceRollResult, string, Action> OnDiceRollRequested;

        // 엔딩 시 플레이 스타일을 칭호 + 수치 한 줄로 요약한다. 코드 규칙 기반, API 호출 없음.
        // 칭호·임계값은 아래 Epithet에서 바로 수정하면 된다. GameSaveData가 저장 타입으로도 쓴다.
        public struct Tally
        {
            public int Safe, Risky, Reckless;
            public int CritFail, Fail, Partial, Success, CritSuccess;

            public void CountChoice(ChoiceRisk risk)
            {
                switch (risk)
                {
                    case ChoiceRisk.Risky: Risky++; break;
                    case ChoiceRisk.Reckless: Reckless++; break;
                    default: Safe++; break;
                }
            }

            public void CountOutcome(DiceOutcome outcome)
            {
                switch (outcome)
                {
                    case DiceOutcome.CriticalFailure: CritFail++; break;
                    case DiceOutcome.Failure: Fail++; break;
                    case DiceOutcome.Partial: Partial++; break;
                    case DiceOutcome.Success: Success++; break;
                    case DiceOutcome.CriticalSuccess: CritSuccess++; break;
                }
            }

            public string Epithet()
            {
                if (Reckless >= Risky && Reckless > Safe) return "무모한 돌격자";
                if (Safe > Risky + Reckless) return "신중한 관찰자";
                if (CritSuccess >= 3) return "운명의 총아";
                if (CritFail >= 3) return "불운한 방랑자";
                if (Risky >= Safe && Risky >= Reckless && Risky > 0) return "계산된 승부사";
                return "즉흥적인 여행자";
            }

            // 엔딩 화면에 그대로 넣는 문자열. 가운데 정렬, 항목별 한 줄씩, 총평(칭호)은 맨 아래에 크게.
            public string Summary() =>
                "<align=center>━ 플레이어 평가 ━\n\n" +
                $"안전 {Safe}\n위험 {Risky}\n무모 {Reckless}\n\n" +
                $"대성공 {CritSuccess}\n대실패 {CritFail}\n\n" +
                $"<size=160%><b>{Epithet()}</b></size></align>";
        }

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
            int? choiceIndex = ParseChoiceInput(text, _lastChoices.Count);
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
                            ChatSender.Narration, FormatChoicesPlain(_lastChoices), DateTime.UtcNow.ToString("o"))
                        {
                            DisplayText = FormatChoicesColored(_lastChoices)
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
                            ChatSender.Narration, _tally.Summary(), DateTime.UtcNow.ToString("o"));
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
                    ChatSender.Narration, FormatChoicesPlain(_lastChoices), DateTime.UtcNow.ToString("o"))
                {
                    DisplayText = FormatChoicesColored(_lastChoices)
                });
            }
        }

        // 플레이어가 입력창에 친 텍스트가 선택지 번호인지(1~choiceCount) 판별한다.
        // 선택지 번호면 0-based 인덱스를, 아니면 null(자유 입력)을 반환.
        private static int? ParseChoiceInput(string text, int choiceCount)
        {
            if (choiceCount <= 0 || string.IsNullOrWhiteSpace(text)) return null;
            if (!int.TryParse(text.Trim(), out int number)) return null;
            if (number < 1 || number > choiceCount) return null;
            return number - 1;
        }

        // 선택지 목록을 채팅에 보여줄 문자열로 만든다.
        // Plain은 대화 히스토리/프롬프트에 저장하는 평문, Colored는 태그 라벨에만 색을 입힌 표시용.
        private static string FormatChoicesPlain(IReadOnlyList<Choice> choices) => FormatChoiceList(choices, colored: false);

        private static string FormatChoicesColored(IReadOnlyList<Choice> choices) => FormatChoiceList(choices, colored: true);

        private static string FormatChoiceList(IReadOnlyList<Choice> choices, bool colored)
        {
            if (choices == null || choices.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < choices.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                var label = ChoiceTagLabel(choices[i].Risk);
                if (colored) label = $"<color={ChoiceTagColor(choices[i].Risk)}>{label}</color>";
                sb.Append($"{i + 1}. {label} {choices[i].Text}");
            }
            return sb.ToString();
        }

        private static string ChoiceTagLabel(ChoiceRisk risk)
        {
            switch (risk)
            {
                case ChoiceRisk.Risky: return "[위험]";
                case ChoiceRisk.Reckless: return "[무모]";
                default: return "[안전]";
            }
        }

        private static string ChoiceTagColor(ChoiceRisk risk)
        {
            switch (risk)
            {
                case ChoiceRisk.Risky: return RiskyChoiceColor;
                case ChoiceRisk.Reckless: return RecklessChoiceColor;
                default: return SafeChoiceColor;
            }
        }
    }
}
