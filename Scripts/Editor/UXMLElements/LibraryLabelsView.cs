using System;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Farbod.Prefabbricato.Backend;
using static System.Net.Mime.MediaTypeNames;
using static Farbod.Prefabbricato.EditorDataManager;
using System.Runtime.Remoting.Messaging;

namespace Farbod.Prefabbricato
{
    public partial class LibraryLabelsView : VisualElement
    {
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;
        private readonly static float TAG_COLOR_MAX_OPACITY = 0.1f;
        private readonly static string m_AssetLabelUssClassName = "prefab-tag";
        private static Background m_LabelIconImage = UIExtensions.GetEditorIcon("d_FilterByLabel");


        private ScrollView m_List;
        private ToolbarSearchField m_SearchField;
        private Dictionary<string, VisualElement> m_DisplayedLabels = new(); //Filtered labels based on search
        private List<LabelData> m_AllLabels = new(); // All registered labels


        internal event Action<string> onLabelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;
        public LibraryLabelsView()
        {
            //Search bar
            m_SearchField = new();
            m_SearchField.RegisterValueChangedCallback(OnSearchTextChanged);
            hierarchy.Add(m_SearchField);

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
            m_DisplayedLabels.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                // Show all labels
                foreach (LabelData label in m_AllLabels)
                {
                    CreateEntry(label.name, label.color, label.latestCount);
                }
                return;
            }

            // Case-insensitive search
            string searchLower = searchText.ToLowerInvariant();

            foreach (LabelData label in m_AllLabels)
            {
                if (label.name.ToLowerInvariant().Contains(searchLower))
                {
                    CreateEntry(label.name, label.color, label.latestCount);
                }
            }
        }

        internal void SetLabels(List<LabelData> labels)
        {
            m_AllLabels = labels; // Store for filtering
            m_DisplayedLabels.Clear();
            m_List.Clear();

            // Show all labels initially
            foreach (LabelData label in labels)
            {
                CreateEntry(label.name, label.color, label.latestCount);
            }
        }

        private VisualElement CreateEntry(string text, Color? color, int counterValue)
        {
            #region template
            var entry = new VisualElement();
            entry.AddToClassList(m_AssetLabelUssClassName);

            var finalColor = color.HasValue ? color.Value : TAG_COLOR_DEFAULT;
            finalColor.a = Mathf.Min(finalColor.a, TAG_COLOR_MAX_OPACITY);
            entry.style.backgroundColor = finalColor;


            //Label icon
            var icon = new VisualElement();
            icon.style.width = icon.style.height = 12;
            icon.style.backgroundImage = m_LabelIconImage;
            entry.Add(icon);

            //Label name
            var label = new Label(text);
            label.style.flexGrow = 1;
            entry.Add(label);

            //Label counter
            var counter = new Label(counterValue.ToString());
            entry.Add(counter);
            #endregion

            #region events
            //Click event
            entry.RegisterCallback<ClickEvent>(evt => onLabelClicked?.Invoke(text));
            //Context menu manipulator
            entry.AddManipulator(new ContextualMenuManipulator(e => onLabelContextMenu?.Invoke(text, e)));
            #endregion

            

            m_List.contentContainer.Add(entry);
            m_DisplayedLabels[name] = label;
            return entry;
        }
    }
}