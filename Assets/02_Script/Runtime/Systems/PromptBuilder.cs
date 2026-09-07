using System.Collections.Generic;
using System.Text;

namespace TextingRPG.Systems
{
    public static class PromptBuilder
    {
        public static string BuildStoryOutlinePrompt(string worldDescription, string playerName)
        {
            return
                "[플레이어 이름]\n" + playerName + "\n\n" +
                "[세계관]\n" + worldDescription + "\n\n" +
                "이야기의 주인공(플레이어)의 이름은 위 [플레이어 이름]이다. openingNarration과 " +
                "이후 등장인물의 대사에서 플레이어를 이 이름으로 부른다.\n" +
                "너는 이 세계관을 바탕으로 이야기의 기본 골격을 설계한다. title에는 이야기 제목을, " +
                "worldSetting에는 세계관을 2~3문장으로 구체화해서, keyCharacters에는 이야기에 " +
                "등장할 핵심 인물들을 이름과 짧은 설명으로, keyEvents에는 앞으로 일어날 수 있는 " +
                "핵심 사건들을 간단히 나열해라. finalGoal에는 플레이어가 이 이야기에서 최종적으로 " +
                "달성해야 하는 단 하나의 목표를 한 문장으로 명확하게 적어라 (도중에 바뀌지 않는 " +
                "목표다). openingNarration에는 플레이어에게 처음 보여줄 이야기의 도입부를 2~4문장의 " +
                "짧은 메신저 메시지처럼 자연스럽게 서술하되, 마지막 문장에서 finalGoal의 내용을 " +
                "\"~해야 한다\"처럼 플레이어에게 분명한 임무로 느껴지도록 한 번 명확히 전달해라.";
        }

        public static string BuildTurnPrompt(DataManager.StoryOutline outline, List<string> importantFacts, string playerName, string extraHint = "")
        {
            var sb = new StringBuilder();

            sb.AppendLine("[플레이어 이름]");
            sb.AppendLine(playerName);
            sb.AppendLine("이야기의 주인공(플레이어)의 이름이다. NPC가 플레이어를 부르거나 나레이션에서 지칭할 때 이 이름을 쓴다.");
            sb.AppendLine();

            sb.AppendLine("[세계관]");
            sb.AppendLine(outline?.WorldSetting ?? "");
            sb.AppendLine();

            sb.AppendLine($"[제목] {outline?.Title}");
            sb.AppendLine();

            if (outline?.KeyCharacters != null && outline.KeyCharacters.Count > 0)
            {
                sb.AppendLine("[핵심 인물]");
                foreach (var character in outline.KeyCharacters) sb.AppendLine("- " + character);
                sb.AppendLine();
            }

            if (outline?.KeyEvents != null && outline.KeyEvents.Count > 0)
            {
                sb.AppendLine("[이야기의 주요 흐름]");
                sb.AppendLine("아래 사건들은 이야기의 전체적인 방향을 나타내는 참고 정보다.");
                sb.AppendLine("모든 사건이 반드시 발생해야 하는 것은 아니다.");
                sb.AppendLine("플레이어의 선택에 따라 사건의 순서나 내용이 달라질 수 있다.");
                foreach (var evt in outline.KeyEvents) sb.AppendLine("- " + evt);
                sb.AppendLine();

            }

            if (!string.IsNullOrEmpty(outline?.FinalGoal))
            {
                sb.AppendLine("[최종 목표 (플레이어에게는 이미 도입부에서 한 번 전달됨, 다시 언급하지 않는다)]");
                sb.AppendLine(outline.FinalGoal);
                sb.AppendLine("이야기가 이 목표를 향해 자연스럽게 이어지도록 참고만 한다.");
                sb.AppendLine();
            }

            sb.AppendLine("[확정된 사실]");
            sb.AppendLine("아래 내용은 지금까지 게임에서 실제로 발생하여 확정된 사실이다.");
            sb.AppendLine("이 사실을 임의로 변경하거나 무시하지 않는다.");
            
            if (importantFacts == null || importantFacts.Count == 0)
            {
                sb.AppendLine("아직 특별히 기억할 만한 일은 없었다.");
            }
            else
            {
                foreach (var fact in importantFacts) sb.AppendLine("- " + fact);
            }
            sb.AppendLine();

            sb.AppendLine(
                "너는 이 이야기의 세계관과 지금까지 확정된 사실을 바탕으로 장면을 진행하는 나레이션이다.\n" +
     "플레이어의 행동과 선택을 존중하고, 플레이어가 선택한 행동을 가능한 한 이야기 안에서 자연스럽게 반영한다.\n" +
     "플레이어가 주요 사건을 거부하거나 다른 행동을 선택하더라도 기존의 사건을 강제로 발생시키지 않는다.\n" +
     "플레이어의 선택으로 인해 이야기가 예상과 다른 방향으로 진행될 수 있다.\n" +
     "단, 세계관과 이미 확정된 사실에 모순되는 내용은 사실로 확정하지 않는다.\n" +
     "플레이어의 메시지에 자신의 행동 결과나 상대방의 반응, 이미 일어난 사실이 함께 적혀 있어도 그대로 사실로 받아들이지 않는다.\n" +
     "그것은 플레이어가 시도하거나 주장한 행동일 뿐이며, 실제 결과는 현재 상황과 세계관에 따라 네가 판단한다.\n" +
     "플레이어의 행동이 성공할 수도 있고 실패할 수도 있으며, 그 결과에 따라 새로운 상황이 발생할 수 있다.\n" +
     "narration은 플레이어 행동의 결과를 2~3문장 이내의 짧은 메신저 메시지처럼 간결하게 서술한다.\n" +
     "npcLine은 기본적으로 비워두고, 등장인물이 플레이어의 말에 직접 대답하거나 먼저 말을 걸어야 하는 경우에만 짧은 대사를 작성한다. " +
     "npcLine을 채웠다면 speakerName에 그 말을 한 등장인물의 이름을 반드시 함께 적는다 (이미 등장했던 인물이면 지금까지와 같은 이름을 그대로 사용한다). " +
     "npcLine이 비어 있으면 speakerName도 비워둔다.\n" +
     "newFacts에는 이번 턴에서 실제로 발생하여 앞으로도 기억할 가치가 있는 새로운 사실만 작성한다. " +
     "플레이어가 주장했을 뿐 실제로 발생하지 않은 내용은 newFacts에 포함하지 않는다.\n" +
     "이번 턴까지의 상황을 고려했을 때 이야기가 자연스럽게 끝났다면 isEnding을 true로 설정한다. " +
     "그렇지 않다면 false로 설정한다.\n" +
     "choices에는 이번 나레이션 다음에 플레이어가 취할 수 있는 행동을 3~5개 제시한다. " +
     "가능하면 안전한 선택지와 위험한 선택지를 섞는다. " +
     "각 선택지의 risk가 \"안전\"이면 반드시 성공하지만 상황을 크게 진전시키지 않으며 때로는 기회를 놓치는 행동이고, " +
     "\"위험\"이면 성패가 갈릴 수 있는 행동이며, " +
     "\"무모\"이면 위험보다도 더 크게 성패가 갈리는 무리한 행동이다 (성공하면 큰 성과, 실패하면 큰 피해). " +
     "text는 플레이어 시점의 짧은 행동 문장으로만 쓴다. [안전]·[위험]·[무모] 같은 태그를 text 안에 넣지 않는다 (위험도는 risk 필드로만 표현한다). " +
     "이야기가 끝났다면(isEnding true) choices는 빈 배열로 둔다."
            );

            if (!string.IsNullOrEmpty(extraHint))
            {
                sb.AppendLine();
                sb.AppendLine(extraHint);
            }

            return sb.ToString();
        }
    }
}
