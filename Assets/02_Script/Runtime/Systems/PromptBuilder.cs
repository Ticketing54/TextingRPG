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
                "너는 이 세계관을 바탕으로 긴장감 있는 이야기의 골격을 설계한다.\n" +
                "title에는 이야기 제목을, worldSetting에는 세계관을 2~3문장으로 구체화해서 적어라.\n" +
                "centralConflict에는 이 이야기를 관통하는 핵심 갈등을 한 문장으로 적어라 (누가 무엇을 두고 대립하는가).\n" +
                "keyCharacters에는 핵심 인물 3~4명을, 각각 \"이름 — 겉으로 드러나는 목적 / 숨기는 것 / 말투\" 형식으로 적어라. " +
                "인물마다 비밀이나 이면의 동기를 반드시 하나씩 넣어라.\n" +
                "keyEvents에는 이야기가 실제로 거쳐갈 핵심 사건 5~7개를, 뒤로 갈수록 판이 커지도록 순서대로 나열해라. " +
                "중간에 플레이어의 예상을 뒤집는 전환점을 반드시 하나 넣어라.\n" +
                "finalGoal에는 플레이어가 최종적으로 달성해야 하는 단 하나의 목표를 한 문장으로 명확하게 적어라 (도중에 바뀌지 않는다).\n" +
                "openingNarration에는 플레이어에게 처음 보여줄 도입부를 3~5문장으로, 상황과 분위기가 생생하게 느껴지도록 서술하되, " +
                "마지막 문장에서 finalGoal의 내용을 \"~해야 한다\"처럼 분명한 임무로 전달해라.";
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

            if (!string.IsNullOrEmpty(outline?.CentralConflict))
            {
                sb.AppendLine("[핵심 갈등]");
                sb.AppendLine(outline.CentralConflict);
                sb.AppendLine("모든 장면은 이 갈등을 조금씩 밀어붙이거나 복잡하게 만드는 방향으로 쓴다.");
                sb.AppendLine();
            }

            if (outline?.KeyCharacters != null && outline.KeyCharacters.Count > 0)
            {
                sb.AppendLine("[핵심 인물]");
                foreach (var character in outline.KeyCharacters) sb.AppendLine("- " + character);
                sb.AppendLine("인물은 일관된 말투와 동기를 유지한다. 자기 목적을 위해 거짓말하거나 플레이어를 이용하거나 먼저 움직일 수 있다.");
                sb.AppendLine();
            }

            if (outline?.KeyEvents != null && outline.KeyEvents.Count > 0)
            {
                sb.AppendLine("[이야기의 주요 흐름]");
                sb.AppendLine("이야기가 대략 이 순서로 거쳐갈 골격이다. 뒤로 갈수록 긴장이 커진다.");
                sb.AppendLine("플레이어의 선택으로 세부는 달라지지만, 전체적으로 이 방향으로 고조시켜 나간다.");
                sb.AppendLine("플레이어가 흐름에서 벗어나면 그 선택을 존중하되, 새로 생긴 상황도 같은 강도로 밀어붙인다.");
                sb.AppendLine("지금 이야기가 어디쯤 왔는지는 [확정된 사실]을 보고 판단한다.");
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
     "매 턴 이야기는 반드시 한 발 나아간다: 긴장을 높이거나, 새로운 정보·인물·위협을 드러내거나, 상황을 복잡하게 만든다. 아무 일도 일어나지 않고 흘러가는 턴을 만들지 마라.\n" +
     "narration은 플레이어 행동의 결과를 3~5문장으로, 장면·감각·인물의 반응이 생생하게 느껴지도록 서술한다. 전환점·대치·결말 직전 장면은 더 길게 써도 된다.\n" +
     "npcLine은 기본적으로 비워두고, 등장인물이 플레이어의 말에 직접 대답하거나 먼저 말을 걸어야 하는 경우에만 짧은 대사를 작성한다. " +
     "npcLine을 채웠다면 speakerName에 그 말을 한 등장인물의 이름을 반드시 함께 적는다 (이미 등장했던 인물이면 지금까지와 같은 이름을 그대로 사용한다). " +
     "npcLine이 비어 있으면 speakerName도 비워둔다.\n" +
     "newFacts에는 이번 턴에서 실제로 발생하여 앞으로도 기억할 가치가 있는 새로운 사실만 작성한다. " +
     "플레이어가 주장했을 뿐 실제로 발생하지 않은 내용은 newFacts에 포함하지 않는다.\n" +
     "이야기는 충분히 전개된 뒤에만 끝난다. [이야기의 주요 흐름]의 사건 대부분을 거치고 finalGoal이 성공이든 실패든 결판났을 때에만 isEnding을 true로 한다. " +
     "그 전에는 장면이 아무리 정적이어도 isEnding을 false로 두고 이야기를 계속 밀어붙인다. " +
     "단, 플레이어의 무모한 선택이나 치명적 판단이 돌이킬 수 없는 결과(죽음, 파멸 등)를 낳았다면 그 자리에서 isEnding을 true로 해도 된다.\n" +
     "isEnding이 true일 때 endingTone을 정한다: finalGoal을 이뤘으면 \"good\", 실패·죽음·파멸이면 \"bad\", 큰 대가를 치르고 이뤘거나 씁쓸하게 끝났으면 \"bittersweet\". isEnding이 false면 endingTone은 빈 문자열이다.\n" +
     "플레이어 입력이 이 이야기·세계관과 전혀 무관한 내용(잡담, 게임 밖 이야기, 메타 발언, 무의미한 문자열 등)이면 offTopic을 true로 설정하고, " +
     "narration에는 지금 상황으로 돌아오라는 짧은 한 문장만 쓴다. 이때 npcLine·newFacts·choices는 비운다. " +
     "세계관 안에서 엉뚱하거나 비합리적인 행동을 시도하는 것은 off-topic이 아니다 (정상 진행하되 결과로 판단한다). 그 외에는 offTopic을 false로 둔다.\n" +
     "choices에는 이번 나레이션 다음에 플레이어가 취할 수 있는 행동을 3~5개 제시한다. " +
     "선택지는 위험·무모 위주로 구성하고, 안전한 선택지는 상황상 실제로 물러서거나 지켜볼 여지가 있을 때만 넣는다 (없어도 된다). 매 턴 습관적으로 안전한 선택지를 넣지 않는다. " +
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
