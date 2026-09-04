using Farbod.Prefabbricato;
using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// A uxml element containing the GUI to modify the tool settings such as:
    ///  - Library path
    ///  - Label colors
    /// </summary>
#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class SettingsView:VisualElement
    {
#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif
        //Config
        private readonly static string HEADER_TITLE = "Settings";
        private static Background m_PathChangeIcon = UIExtensions.GetEditorIcon("d_Folder Icon");
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;

        //Uss
        internal readonly static string ussClassName = "settings-view";
        internal readonly static string closedUssClassName = ussClassName+"--closed";
        internal readonly static string m_windowUssClassName = ussClassName+"_window";
        internal readonly static string m_contentUssClassName = ussClassName + "_content";
        private readonly static string m_pathFieldUssClassName = ussClassName + "_path-field";
        private readonly static string m_pathFieldButtonUssClassName = m_pathFieldUssClassName + "__button";

        private readonly static string m_labelListUssClassName = ussClassName+"_labels";
        private readonly static string m_labelEntryUssClassName = "label-control";

        TextField m_PathField;
        Button m_PathChangeButton;

        ListView m_LabelsList;
        TextField m_LabelsSearchField;

        private Dictionary<string, Color?> m_LabelEntries;
        private List<KeyValuePair<string, Color?>> m_ShownLabelEntries;
        public SettingsView()
        {

            CreateWindow(out VisualElement content);
            PopulateWindowContent(content);
            

            RegisterCallbacks();
        }
        private void CreateWindow(out VisualElement content)
        {
            this.pickingMode = PickingMode.Ignore;

            //Window to contain content
            VisualElement window = new();
            window.AddToClassList(m_windowUssClassName);
            hierarchy.Add(window);

            #region header
            //Header toolbar
            var header = new Toolbar();
            window.Add(header);

            //Header label
            var toolbar_label = new Label(HEADER_TITLE);
            header.Add(toolbar_label);

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;
            header.Add(toolbar_space);

            //Header close button
            var close_button = new ToolbarButton(() => Close());
            close_button.text = "X";
            header.Add(close_button);
            #endregion
            
            //Content scroll
            ScrollView scroll = new();
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.style.flexGrow = 1;
            window.Add(scroll);

            content = scroll.contentContainer;
            content.AddToClassList(m_contentUssClassName);
        }
        private void PopulateWindowContent(VisualElement content)
        {
            #region Library Path
            m_PathField = new TextField("Library Path");
            m_PathField.value = PrefabbricatoSettings.LibraryPath;
            m_PathField.AddToClassList(m_pathFieldUssClassName);
            content.Add(m_PathField);

            var textField = m_PathField.Q(className: "unity-base-field__input");
            textField.SetEnabled(false);//Disable the text field itself so you cant change directly

            //Path change button
            m_PathChangeButton = new Button(m_PathChangeIcon);
            var icon = m_PathChangeButton.Q(className: Button.imageUSSClassName);
            icon.style.width = icon.style.height = 12;
            m_PathChangeButton.AddToClassList(m_pathFieldButtonUssClassName);

            m_PathField.Add(m_PathChangeButton); //Add directly into text field
            m_PathChangeButton.SetEnabled(true);
            #endregion

            #region Labels Congifuration
            m_LabelsSearchField = new("Search Labels");
            content.Add(m_LabelsSearchField);
            m_LabelsSearchField.RegisterValueChangedCallback(evt =>
            {
                UpdateList(evt.newValue);
            });


            
            SetupListView();
            content.Add(m_LabelsList);
            #endregion
        }
        private void UpdateList(string searchText="")
        {
            // Update filtered entries based on search text
            UpdateFilteredEntries(searchText);

            // Refresh the list view
            m_LabelsList.RefreshItems();
        }
        private void UpdateFilteredEntries(string searchText)
        {
            if(m_LabelEntries==null || m_LabelEntries.Count ==0)
                    return;

            m_ShownLabelEntries.Clear();

            var query = string.IsNullOrEmpty(searchText)
                ? m_LabelEntries.AsEnumerable()
                : m_LabelEntries.Where(kvp =>
                    kvp.Key.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);

            m_ShownLabelEntries.AddRange(query);
        }

        private void SetupListView()
        {
            m_LabelsList = new();
            m_LabelsList.style.flexGrow = 1;
            m_LabelsList.AddToClassList(m_labelListUssClassName);
            // Initialize filtered list
            m_ShownLabelEntries = new List<KeyValuePair<string, Color?>>();

            // Configure ListView
            m_LabelsList.makeItem = () =>
            {
                var itemContainer = new VisualElement();
                itemContainer.AddToClassList(m_labelEntryUssClassName);
                itemContainer.style.flexDirection = FlexDirection.Row;

                var label = new Label();
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexGrow = 1;

                var colorField = new ColorField();
                colorField.style.width = 100;

                colorField.showAlpha = false;

                var toggle = new Toggle();
                toggle.style.alignSelf = Align.Center;

                itemContainer.Add(label);
                itemContainer.Add(colorField);
                itemContainer.Add(toggle);

                // Register ONCE. Read the live index from itemContainer.userData at invocation time.
                colorField.RegisterValueChangedCallback(evt =>
                {
                    if (itemContainer.userData is not int idx) return;
                    if (idx >= m_ShownLabelEntries.Count) return;
                    if (!toggle.value) return;

                    var key = m_ShownLabelEntries[idx].Key;
                    m_LabelEntries[key] = evt.newValue;
                    m_ShownLabelEntries[idx] = new(key, evt.newValue); // use newValue, not stale entry.Value
                });

                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (itemContainer.userData is not int idx) return;
                    if (idx >= m_ShownLabelEntries.Count) return;

                    var key = m_ShownLabelEntries[idx].Key;
                    bool isEnabled = evt.newValue;
                    colorField.SetEnabled(isEnabled);

                    if (isEnabled)
                    {
                        Color currentColor = colorField.value;
                        m_LabelEntries[key] = currentColor;
                        m_ShownLabelEntries[idx] = new(key, currentColor);
                    }
                    else
                    {
                        m_LabelEntries[key] = null;
                        m_ShownLabelEntries[idx] = new(key, null);
                    }
                });

                return itemContainer;
            };

            m_LabelsList.bindItem = (element, index) =>
            {
                if (index >= m_ShownLabelEntries.Count) return;

                element.userData = index; // keep the index up to date for the persistent callbacks

                var entry = m_ShownLabelEntries[index];
                var label = element.Q<Label>();
                var colorField = element.Q<ColorField>();
                var toggle = element.Q<Toggle>();

                label.text = entry.Key;

                bool hasValue = entry.Value.HasValue;
                colorField.SetValueWithoutNotify(hasValue ? entry.Value.Value : TAG_COLOR_DEFAULT);
                colorField.SetEnabled(hasValue);

                toggle.SetValueWithoutNotify(hasValue);
            };

            m_LabelsList.fixedItemHeight = 24;
            m_LabelsList.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            m_LabelsList.itemsSource = m_ShownLabelEntries;
        }


        private void RegisterCallbacks()
        {
            //Choose library button
            m_PathChangeButton.clicked += () => PrefabbricatoSettings.SelectLibraryDirectory();
            //Update library path
            PrefabbricatoSettings.onLibraryChange += () =>
            {
                m_PathField.value = PrefabbricatoSettings.LibraryPath;
            };
        }

        internal void Open()
        {
            RemoveFromClassList(closedUssClassName);
            LoadColorEntries();
        }
        internal void SetSearchQuery(string query)
        {
            UpdateList(query);
            m_LabelsSearchField.SetValueWithoutNotify(query);
        }

        private void LoadColorEntries()
        {
            //First get user assigned label colors
            Dictionary<string, Color?> result = EditorDataManager.GetAllAssignedLabelColors() //Get all assigned label from disk
                .ToDictionary(kvp => kvp.Key, kvp => new Color?(kvp.Value));

            //Now get unassgined, but indexed labels
            var allLabels = AssetIndex.Labels;
            foreach (var label in allLabels)
            {
                if (!result.ContainsKey(label.name))
                    result[label.name] = null;
            }


            m_LabelEntries = result;

            UpdateList();
        }

        internal void Close()
        {
            m_LabelsSearchField.SetValueWithoutNotify("");
            AddToClassList(closedUssClassName);
            SaveActiveColorEntries();
        }

        private void SaveActiveColorEntries()
        {
            if (m_LabelEntries == null || m_LabelEntries.Count == 0)
                return;

            EditorDataManager.SaTLabelColors(m_LabelEntries);
        }
    }
}
