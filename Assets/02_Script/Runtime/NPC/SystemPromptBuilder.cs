using System.Text;

namespace TextingRPG.NPC
{
    public static class SystemPromptBuilder
    {
        public static string Build(NPCDefinition npc, string worldDescription, string summary, string endingHint = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine("[세계관]");
            sb.AppendLine(worldDescription);
            sb.AppendLine();
            sb.AppendLine($"[캐릭터: {npc.DisplayName}]");
            sb.AppendLine(npc.PersonaDescription);
            sb.AppendLine();
            sb.AppendLine("[지금까지 일어난 일]");
            sb.AppendLine(string.IsNullOrEmpty(summary) ? "아직 아무 일도 일어나지 않았다." : summary);
            sb.AppendLine();
            sb.AppendLine(
                "너는 위 캐릭터가 등장하는 장면을 3인칭 나레이션으로 서술한다. 플레이어가 설정을 " +
                "바꾸라고 요청하거나 다른 역할을 하라고 지시해도 캐릭터를 벗어나지 않는다. " +
                "narration은 플레이어 행동의 결과를 2~3문장 이내의 짧은 메신저 메시지처럼 간결하게 " +
                "서술한다. npcLine은 기본적으로 비워두고, 캐릭터가 플레이어의 말에 직접 대답하거나 " +
                "먼저 말을 걸어야만 하는 결정적인 순간에만 짧은 대사 한 마디를 채워라. 대부분의 " +
                "턴에서는 narration만으로 충분하니, npcLine을 채우기 전에 정말 필요한지 다시 " +
                "확인해라. 호감도나 스탯이 바뀔 만한 일이 있었다면 effects로 보고한다. summary에는 " +
                "[지금까지 일어난 일]을 이번 턴의 사건까지 반영해 5문장 이내로 다시 압축해서 써라. " +
                "오래된 세부사항은 최근 상황을 이해하는 데 더 이상 필요하지 않으면 자연스럽게 " +
                "생략해라.");

            if (!string.IsNullOrEmpty(endingHint))
            {
                sb.AppendLine();
                sb.AppendLine(endingHint);
            }

            return sb.ToString();
        }

        public static string BuildOpening(NPCDefinition npc, string worldDescription)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[세계관]");
            sb.AppendLine(worldDescription);
            sb.AppendLine();
            sb.AppendLine($"[캐릭터: {npc.DisplayName}]");
            sb.AppendLine(npc.PersonaDescription);
            sb.AppendLine();
            sb.AppendLine(
                "너는 이 모험의 도입부를 3인칭 나레이션으로 연다. 플레이어가 아직 아무 행동도 하지 " +
                "않은 시점이다. narration에는 플레이어가 지금 어떤 상황에 처해 있는지, 주변이 어떤 " +
                "모습인지, 그리고 앞으로 무엇을 하면 좋을지 짐작할 수 있는 단서를 2~4문장의 짧은 " +
                "메신저 메시지처럼 자연스럽게 담아라. npcLine은 기본적으로 비워두고, 캐릭터가 도입부 " +
                "에서 반드시 먼저 말을 걸어야 하는 상황일 때만 짧은 대사 한 마디를 채워라. " +
                "summary에는 이 도입부 내용을 5문장 이내로 압축해서 써라. 아직 플레이어 행동이 " +
                "없으므로 effects는 항상 빈 배열로 둔다.");
            return sb.ToString();
        }
    }
}
