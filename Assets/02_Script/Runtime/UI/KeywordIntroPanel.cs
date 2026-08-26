using System;
using System.Collections.Generic;
using TextingRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TextingRPG.UI
{
    public class KeywordIntroPanel : MonoBehaviour
    {
        [SerializeField] KeywordSet keywordSet;
        [SerializeField] Transform keywordButtonContainer;
        [SerializeField] Button keywordButtonPrefab;
        [SerializeField] Button startButton;
        [SerializeField] int requiredSelectionCount = 3;

        public event Action<string[]> OnKeywordsConfirmed;

        private readonly List<string> _selected = new List<string>();

        private void Awake()
        {
            foreach (var keyword in keywordSet.Keywords)
            {
                var button = Instantiate(keywordButtonPrefab, keywordButtonContainer);
                button.GetComponentInChildren<TMP_Text>().text = keyword;
                button.onClick.AddListener(() => ToggleKeyword(keyword, button));
            }

            startButton.onClick.AddListener(ConfirmSelection);
            startButton.interactable = false;
        }

        private void ToggleKeyword(string keyword, Button button)
        {
            if (_selected.Contains(keyword))
            {
                _selected.Remove(keyword);
                SetButtonSelectedVisual(button, false);
            }
            else if (_selected.Count < requiredSelectionCount)
            {
                _selected.Add(keyword);
                SetButtonSelectedVisual(button, true);
            }

            startButton.interactable = _selected.Count == requiredSelectionCount;
        }

        private static void SetButtonSelectedVisual(Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? new Color(0.75f, 0.85f, 1f) : Color.white;
            button.colors = colors;
        }

        private void ConfirmSelection()
        {
            OnKeywordsConfirmed?.Invoke(_selected.ToArray());
            gameObject.SetActive(false);
        }
    }
}
