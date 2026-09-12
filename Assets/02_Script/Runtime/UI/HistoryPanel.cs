using System;
using System.Globalization;
using TextingRPG.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    // 완료된 게임 목록을 보여주고, 하나를 고르면 그 id를 알린다.
    // entryButtonPrefab은 씬에서 비활성 상태로 보관되는 템플릿 (entryContainer 밖에 둔다).
    public class HistoryPanel : MonoBehaviour
    {
        [SerializeField] Transform entryContainer;
        [SerializeField] Button entryButtonPrefab;
        [SerializeField] Button backButton;
        [SerializeField] GameObject emptyLabel;

        public event Action<string> OnEntrySelected;
        public event Action OnBackClicked;

        private void Awake()
        {
            backButton.onClick.AddListener(() => OnBackClicked?.Invoke());
        }

        // 패널을 보여줄 때마다 호출해 최신 목록으로 갱신한다.
        public void Populate()
        {
            for (int i = entryContainer.childCount - 1; i >= 0; i--)
                Destroy(entryContainer.GetChild(i).gameObject);

            var entries = SaveSystem.LoadHistoryIndex().Entries;
            if (emptyLabel != null) emptyLabel.SetActive(entries.Count == 0);

            foreach (var entry in entries)
            {
                var button = Instantiate(entryButtonPrefab, entryContainer);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = FormatEntry(entry);

                var id = entry.Id;
                button.onClick.AddListener(() => OnEntrySelected?.Invoke(id));
            }
        }

        private static string FormatEntry(HistoryEntry entry)
        {
            var tone = entry.EndingTone switch
            {
                "good" => "해피엔딩",
                "bad" => "배드엔딩",
                "bittersweet" => "씁쓸한 엔딩",
                _ => "중단됨"
            };

            var date = DateTime.TryParse(entry.SavedAtIso, null, DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "";

            return $"{entry.Title}  ·  {tone}  ·  {date}";
        }
    }
}
