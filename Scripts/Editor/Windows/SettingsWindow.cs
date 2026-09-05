using Farbod.Prefabbricato.Backend;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    public class SettingsWindow : EditorWindow
    {
        //Window Config
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/SettingsWindowStyle";
        static readonly string WINDOW_ICON_CONTENT = "Settings@2x";
        static readonly string WINDOW_TITLE = "Prefabbricato Settings";
        static readonly Vector2 WINDOW_MIN_SIZE = new(260, 180);
        static readonly Vector2 WINDOW_MAX_SIZE = new(400, 500);

        //Config
        private readonly static Color LABEL_COLOR_DEFAULT = Color.mediumAquamarine;
        private const string LABELS_LIST_EMPTY_TEXT = "No labels registered";
        private static string LABELS_LIST_NO_RESULT(string query) => $"No result for '{query}'";

        //Uss
        internal readonly static string ussClassName = "settings-view";
        private readonly static string m_pathFieldUssClassName = ussClassName + "_path-field";
        private readonly static string m_pathFieldButtonUssClassName = m_pathFieldUssClassName + "__button";

        private readonly static string m_labelListUssClassName = ussClassName + "_labels";
        private readonly static string m_labelEntryUssClassName = "label-control";

        TextField m_PathField;
        Button m_PathChangeButton;

        ListView m_LabelsList;
        Label m_ListEmptyLabel;
        ToolbarSearchField m_LabelsSearchField;

        private Dictionary<string, Color?> m_LabelEntries;
        private List<KeyValuePair<string, Color?>> m_ShownLabelEntries;


        private VisualElement m_Root;

        /// <summary>
        /// The menu item available in the editor toolbar for opening this window.
        /// </summary>
        ///[MenuItem("Tools/Prefabbricato Settings")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<SettingsWindow>();
            var icon = EditorGUIUtility.IconContent(WINDOW_ICON_CONTENT).image;
            wnd.titleContent = new GUIContent(WINDOW_TITLE, icon);
            wnd.minSize = WINDOW_MIN_SIZE;
            wnd.maxSize = WINDOW_MAX_SIZE;
        }

        private void OnDestroy()
        {
            m_LabelsSearchField.SetValueWithoutNotify("");
            SaveActiveColorEntries();
        }

        /// <summary>
        /// The main function that is called when this window is created.
        /// </summary>
        protected virtual void CreateGUI()
        {
            m_Root = base.rootVisualElement;

            //Load default stylesheet
            StyleSheet style = Resources.Load<StyleSheet>(STYLESHEET_RESOURCE_PATH);
            Debug.Assert(style != null, $"[{WINDOW_TITLE}] Stylesheet not found at {STYLESHEET_RESOURCE_PATH}");
            m_Root.styleSheets.Add(style);


            m_Root.AddToClassList(ussClassName);
            PopulateWindowContent(m_Root);
            RegisterCallbacks();

            FetchProjectLabels();

            //m_Root.schedule.Execute(() =>
            //{
            //    m_Root.AddToClassList(ussClassName);
            //    PopulateWindowContent(m_Root);
            //    RegisterCallbacks();

            //    RefreshColorEntries();
            //});
        }

        private void PopulateWindowContent(VisualElement content)
        {
            #region Library Path
            m_PathField = new TextField("Library Path");
            //Set initial value
            m_PathField.value = PrefabbricatoSettings.LibraryPath;
            m_PathField.AddToClassList(m_pathFieldUssClassName);

            var textField = m_PathField.Q(className: "unity-base-field__input");
            textField.SetEnabled(false);//Disable the text field itself so you cant change directly

            //Path change button
            m_PathChangeButton = new Button(UIExtensions.GetEditorIcon("Folder Icon"));
            var icon = m_PathChangeButton.Q(className: Button.imageUSSClassName);
            icon.style.width = icon.style.height = 12;
            m_PathChangeButton.AddToClassList(m_pathFieldButtonUssClassName);
            m_PathChangeButton.SetEnabled(true);

            #endregion

            #region Labels Congifuration
            Label section_heading = new Label("Label Colors");
            section_heading.AddToClassList(ussClassName + "_labels-section-header");

            m_LabelsSearchField = new();
            m_LabelsSearchField.style.width = new StyleLength(StyleKeyword.Auto);
            m_LabelsSearchField.RegisterValueChangedCallback(evt =>
            {
                UpdateListView(evt.newValue);
            });


            SetupListView();

            content.Add(m_PathField);
            m_PathField.Add(m_PathChangeButton); //Add directly into text field

            content.Add(section_heading);
            content.Add(m_LabelsSearchField);
            content.Add(m_LabelsList);
            #endregion
        }

        private void UpdateListView(string query = null)
        {
            // Update filtered entries based on search text
            FiltersShownEntries(query ?? m_LabelsSearchField.value);
            //Ensure search field matches new query.
            //dont notify since m_LabelsSearchField calls this method on ValueChanged
            m_LabelsSearchField?.SetValueWithoutNotify(query);

            // Refresh the list view
            m_LabelsList.RefreshItems();

            //If there are no entries being shown
            if (m_ShownLabelEntries.Count == 0)
            {
                m_ListEmptyLabel = m_LabelsList.Q<Label>(className: "unity-list-view__empty-label");


                //If there are label entries but queryturns up nothing
                if (m_ShownLabelEntries.Count == 0 && m_LabelEntries.Count != 0)
                    m_ListEmptyLabel.text = LABELS_LIST_NO_RESULT(query);
                else
                    m_ListEmptyLabel.text = LABELS_LIST_EMPTY_TEXT;
            }
        }
        /// <summary>
        /// Filter m_LabelEntries(all) by search query and add them to m_ShownLabelEntries(filtered)
        /// </summary>
        /// <param name="searchText"></param>
        private void FiltersShownEntries(string searchText)
        {
            if (m_LabelEntries == null || m_LabelEntries.Count == 0)
                return;

            m_ShownLabelEntries.Clear();

            var query = string.IsNullOrEmpty(searchText)
                ? m_LabelEntries.AsEnumerable()
                : m_LabelEntries.Where(kvp =>
                    kvp.Key.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0);

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

                    SaveActiveColorEntries();
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

                    SaveActiveColorEntries();
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
                colorField.SetValueWithoutNotify(hasValue ? entry.Value.Value : LABEL_COLOR_DEFAULT);
                colorField.SetEnabled(hasValue);

                toggle.SetValueWithoutNotify(hasValue);
            };

            m_LabelsList.fixedItemHeight = 24;
            m_LabelsList.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            m_LabelsList.itemsSource = m_ShownLabelEntries;
            m_LabelsList.selectionType = SelectionType.Single;
        }

        private void FetchProjectLabels()
        {
            //First get user assigned label colors
            m_LabelEntries = LabelUtilities.GetProjectLabels(LabelSelection.IndexedAndColorAssigned);
            UpdateListView();
        }
        private void SaveActiveColorEntries()
        {
            if (m_LabelEntries == null || m_LabelEntries.Count == 0)
                return;

            PrefabbricatoSettings.instance.AssignColorToLabels(m_LabelEntries);
        }

        private void RegisterCallbacks()
        {
            //Choose library double click
            m_PathField.labelElement.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount > 1)
                    PrefabbricatoSettings.SelectLibraryDirectory();
            });
            m_PathField.tooltip = m_PathField.value;
            m_PathField.RegisterValueChangedCallback(e => { m_PathField.tooltip = e.newValue; });

            //Choose library button
            m_PathChangeButton.clicked += () => PrefabbricatoSettings.SelectLibraryDirectory();
            //Update library path
            PrefabbricatoSettings.onLibraryChange += (p) => m_PathField.value = p;
        }
        private void PingLabelInList(string label)
        {
            UpdateListView("");
            int index = m_ShownLabelEntries.FindIndex(kvp => kvp.Key == label);
            if (index >= 0 && index < m_ShownLabelEntries.Count)
            {
                m_LabelsList.ScrollToItem(index);
                m_LabelsList.SetSelection(index);
            }

        }
        /// <summary>
        /// Select and scroll to a specific label, so the user can easily find it.
        /// </summary>
        /// <param name="label"></param>
        public static void PingLabel(string label)
        {
            ShowWindow();
            var wnd = GetWindow<SettingsWindow>();
            wnd.PingLabelInList(label);
        }
    }
}