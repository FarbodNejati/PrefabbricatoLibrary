using System;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Farbod.Prefabbricato.Backend;
using static System.Net.Mime.MediaTypeNames;
using static Farbod.Prefabbricato.IndexSavedDataManager;
using System.Runtime.Remoting.Messaging;
using static UnityEngine.Audio.ProcessorInstance.AvailableData;
using System.Reflection;
using System.Linq;

namespace Farbod.Prefabbricato
{
    public partial class LibraryLabelsView : VisualElement
    {
        private readonly static Color LABEL_COLOR_DEFAULT = Color.lightBlue;
        private readonly static float TAG_COLOR_MAX_OPACITY = 0.15f;
        private readonly static string m_AssetLabelUssClassName = "prefab-tag";
        private readonly static string m_LabelNameUssClassName = m_AssetLabelUssClassName + "_name";
        private readonly static string m_LabelCounterUssClassName = m_AssetLabelUssClassName+"_counter";

        private static Background m_LabelIconImage = UIExtensions.GetEditorIcon("FilterByLabel");


        private ListView m_List;
        private ToolbarSearchField m_SearchField;
        private Dictionary<string, Color?> m_LabelEntries;
        private List<KeyValuePair<string, Color?>> m_ShownLabelEntries;


        internal event Action<string> onLabelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;
        public LibraryLabelsView()
        {
            //Search bar
            m_SearchField = new();
            m_SearchField.RegisterValueChangedCallback(OnSearchTextChanged);
            hierarchy.Add(m_SearchField);

            //Scroll for content
            m_List = new();
            hierarchy.Add(m_List);
            m_List.fixedItemHeight = 24;
            m_List.style.flexGrow = 1;
            m_List.selectionType = SelectionType.None;
            //List view functionality
            m_ShownLabelEntries = new();
            m_List.makeItem = CreateEntry;
            m_List.bindItem = BindEntry;
            m_List.itemsSource = m_ShownLabelEntries;
        }
        private VisualElement CreateEntry()
        {
            #region template
            var container = new VisualElement();

            var entry = new VisualElement();
            container.Add(entry);
            entry.AddToClassList(m_AssetLabelUssClassName);

            //Label icon
            var icon = new VisualElement();
            icon.style.width = icon.style.height = 12;
            icon.style.backgroundImage = m_LabelIconImage;
            entry.Add(icon);

            //Label name
            var nameLabel = new Label("label");
            nameLabel.AddToClassList(m_LabelNameUssClassName);
            nameLabel.style.flexGrow = 1;
            entry.Add(nameLabel);

            //Label counter
            var counter = new Label("N/A");
            counter.AddToClassList(m_LabelCounterUssClassName);
            entry.Add(counter);
            #endregion

            #region events
            //Click event
            entry.RegisterCallback<ClickEvent>(evt =>
            {
                //User data should hold the index of the item calling this event.

                //Check if user data is integer
                if (container.userData is not int idx) return;
                //Check if index is in range
                if (idx >= m_ShownLabelEntries.Count) return;


                //Get label name by index
                var labelName = m_ShownLabelEntries[idx].Key;
                onLabelClicked?.Invoke(labelName);
            });
            //Context menu manipulator
            entry.AddManipulator(new ContextualMenuManipulator(e =>
            {
                //User data should hold the index of the item calling this event.

                //Check if user data is integer
                if (container.userData is not int idx) return;
                //Check if index is in range
                if (idx >= m_ShownLabelEntries.Count) return;


                //Get label name by index
                var labelName = m_ShownLabelEntries[idx].Key;
                onLabelContextMenu?.Invoke(labelName, e);
            }));
            #endregion

            return container;
        }
        private void BindEntry(VisualElement element, int index)
        {
            if (index >= m_ShownLabelEntries.Count) return;

            ///Assign index to user data
            ///This is then used by the callback events set up inside CreateEntry() to figure out which
            ///Label an event originates from
            element.userData = index;


            var entry = m_ShownLabelEntries[index];

            string name = entry.Key;
            var color = entry.Value;
            //Set label entry background color

            var modifiedColor = color.HasValue ? color.Value : LABEL_COLOR_DEFAULT;
            modifiedColor.a = Mathf.Min(modifiedColor.a, TAG_COLOR_MAX_OPACITY);
            element.Q(className:m_AssetLabelUssClassName).style.backgroundColor = modifiedColor; //Dont apply directly to list entry

            //Set label name
            element.Q<Label>(className: m_LabelNameUssClassName).text = name;

            //Set label counter
            AssetIndex.LabelToAssetIndex.TryGetValue(name, out var hashset);//Get Asset count
            element.Q<Label>(className: m_LabelCounterUssClassName).text = 
                hashset!=null?hashset.Count.ToString():"N/A";
        }

        private void OnSearchTextChanged(ChangeEvent<string> evt)
        {
            string searchText = evt.newValue?.Trim() ?? string.Empty;
            FilterLabels(searchText);
            m_List.RefreshItems();
        }

        private void FilterLabels(string searchText)
        {
            if (m_LabelEntries == null || m_LabelEntries.Count == 0)
                return;

            m_ShownLabelEntries.Clear();

            var query = string.IsNullOrEmpty(searchText)
                ? m_LabelEntries.AsEnumerable()
                : m_LabelEntries.Where(kvp =>
                    kvp.Key.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            m_ShownLabelEntries.AddRange(query);
        }

        public void SetLabels(Dictionary<string, Color?> labels)
        {
            m_LabelEntries = labels;
            FilterLabels(m_SearchField.value);
            m_List.RefreshItems();
        }
    }
}