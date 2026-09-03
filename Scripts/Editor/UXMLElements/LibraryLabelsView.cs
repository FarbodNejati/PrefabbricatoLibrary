using System;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Farbod.Prefabbricato.Backend;
using static System.Net.Mime.MediaTypeNames;
using static Farbod.Prefabbricato.EditorDataManager;

namespace Farbod.Prefabbricato
{
    public partial class LibraryLabelsView : VisualElement
    {
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;
        private readonly static float TAG_COLOR_MAX_OPACITY = 0.1f;
        private readonly static string m_AssetLabelUssClassName = "prefab-tag";



        private ScrollView m_List;
        private ToolbarSearchField m_searchField;
        private Dictionary<string, VisualElement> displayedLabels = new(); //Filtered labels based on search
        private List<LabelData> allLabels = new(); // All registered labels

        internal event Action<string> onLabelClicked;
        public LibraryLabelsView()
        {
            //Search bar
            m_searchField = new();
            m_searchField.RegisterValueChangedCallback(OnSearchTextChanged);
            hierarchy.Add(m_searchField);

            //Scroll for content
            m_List = new ScrollView();
            hierarchy.Add(m_List);
        }
        private void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            string searchText = evt.newValue?.Trim() ?? string.Empty;
            FilterLabels(searchText);
        }

        private void FilterLabels(string searchText)
        {
            m_List.Clear();
            displayedLabels.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                // Show all labels
                foreach (LabelData label in allLabels)
                {
                    CreateEntry(label.name, label.color, label.latestCount);
                }
                return;
            }

            // Case-insensitive search
            string searchLower = searchText.ToLowerInvariant();

            foreach (LabelData label in allLabels)
            {
                if (label.name.ToLowerInvariant().Contains(searchLower))
                {
                    CreateEntry(label.name, label.color, label.latestCount);
                }
            }
        }

        internal void SetLabels(List<LabelData> labels)
        {
            allLabels = labels; // Store for filtering
            displayedLabels.Clear();
            m_List.Clear();

            // Show all labels initially
            foreach (LabelData label in labels)
            {
                CreateEntry(label.name, label.color, label.latestCount);
            }
        }

        private VisualElement CreateEntry(string text, Color? color, int counterValue)
        {
            var entry = new VisualElement();
            entry.RegisterCallback<ClickEvent>(evt => onLabelClicked?.Invoke(text));


            var finalColor = color.HasValue ? color.Value : TAG_COLOR_DEFAULT;
            finalColor.a = Mathf.Min(finalColor.a, TAG_COLOR_MAX_OPACITY);

            entry.style.backgroundColor = finalColor;
            entry.AddToClassList(m_AssetLabelUssClassName);

            var label = new Label(text);
            entry.Add(label);

            var counter = new Label(counterValue.ToString());
            entry.Add(counter);

            m_List.contentContainer.Add(entry);
            displayedLabels[name] = label;
            return entry;
        }
    }
}