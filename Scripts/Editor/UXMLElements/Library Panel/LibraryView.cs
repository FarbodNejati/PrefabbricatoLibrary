using System;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Farbod.Prefabbricato.Backend;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// The library view seen under the inspector panel.
    /// Used to show an overview of the library.
    /// </summary>
#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class LibraryView : VisualElement
    {
        //Config
        private readonly static string HEADER_TITLE = "Library";
        private readonly static string TAB_PROJECT_FILES_TITLE = "Project";
        private readonly static string TAB_ASSET_LABELS_TITLE = "Labels";
        private readonly static string SCAN_WARNING_MSG = "Library scan needed!";
        private readonly static string STYLESHEET_RESOURCE_PATH = "Style/LibraryPaneStyle";

        //USS-Class names
        internal readonly static string ussClassName = "library-view";
        internal readonly static string projectTabUssClassName = ussClassName+"_project-tab";
        private readonly static string projectTabContentUssClassName = projectTabUssClassName + "__content";
        internal readonly static string labelsTabUssClassName = ussClassName + "_labels-tab";
        private readonly static string labelsTabContentUssClassName = labelsTabUssClassName + "__content";
        internal readonly static string ScanWarningPromptUssClassName = ussClassName+"_scan-warning";
        private TabView m_TabView;

        internal LibraryLabelsView LabelsView { get; private set; }
        internal LibraryProjectView ProjectView { get; private set; }

        private ToolbarMenu m_ScanToolbarDropdown;
        private VisualElement m_ScanWarningMessage;
        private Label m_ScanWarningPromptText;

        //internal event Action onSettingsButtonClick;

#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif


        public LibraryView()
        {
            PopulateElement();

            ShowScanWarningPrompt(true);

            //Load default stylesheet
            StyleSheet style = Resources.Load<StyleSheet>(STYLESHEET_RESOURCE_PATH);
            Debug.Assert(style != null, $"[{this.GetType().Name}] Stylesheet not found at {STYLESHEET_RESOURCE_PATH}");
            styleSheets.Add(style);
        }

        private void PopulateElement()
        {
            AddToClassList(ussClassName);
            ///----------------------------------------------
            ///-------------  Header Toolbar  ---------------
            ///----------------------------------------------
            var toolbar = new Toolbar();
            hierarchy.Add(toolbar);

            //Header label
            var toolbar_label = new Label(HEADER_TITLE);
            toolbar.Add(toolbar_label);

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;
            toolbar.Add(toolbar_space);

            //Scan dropdown
            m_ScanToolbarDropdown = new ToolbarMenu();
            m_ScanToolbarDropdown.Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage = new StyleBackground(UIExtensions.GetEditorIcon("Refresh@2x"));
            m_ScanToolbarDropdown.tooltip = "Scan";
            CreateScanMenuOptions(m_ScanToolbarDropdown.menu);
            toolbar.Add(m_ScanToolbarDropdown);


            //Settings button
            var settingsButton = new ToolbarButton(() => SettingsWindow.ShowWindow()) ;
            settingsButton.iconImage = UIExtensions.GetEditorIcon("Settings@2x");
            settingsButton.tooltip = "Settings";
            toolbar.Add(settingsButton);

            #region scan needed prompt
            //Scan warning prompt (shows up next to tool menu to point at it)
            CreateScanWarningMessage(toolbar);
            #endregion

            ///----------------------------------------------
            ///----------------  Tab View  ------------------
            m_TabView = new();
            m_TabView.style.flexGrow = 1;
            m_TabView.contentContainer.style.flexGrow = 1;
            hierarchy.Add(m_TabView);

            //Asset labels tab
            var assetLabelsTab = AddTab(TAB_ASSET_LABELS_TITLE, projectTabUssClassName);
            LabelsView = new();
            LabelsView.name = "labels-view";
            assetLabelsTab.Add(LabelsView);

            //Project tab (folders and directories)
            var projectDirTab = AddTab(TAB_PROJECT_FILES_TITLE, projectTabUssClassName);
            ProjectView = new(PrefabbricatoSettings.LibraryPath, true);
            ProjectView.name = "project-view";
            projectDirTab.Add(ProjectView);


            m_TabView.MakeHeaderStyleButtonGroup();
        }

        /// <summary>
        /// Create a tab, with a scroll view inside and a visual element to contain its content
        /// </summary>
        /// <param name="title"></param>
        /// <param name="tabView"></param>
        /// <param name="ussClassName"></param>
        /// <returns></returns>
        Tab AddTab(string title, string ussClassName)
        {
            var tab = new Tab(title);
            tab.AddToClassList(ussClassName);

            //Setting flex frow to 1 so the tab fill the entire panel
            tab.style.flexGrow = 1;
            tab.contentContainer.style.flexGrow = 1;

            m_TabView.Add(tab);
            return tab;
        }

        
        private void CreateScanMenuOptions(DropdownMenu menu)
        {
            menu.ClearItems();
            menu.AppendAction($"Scan", a => AssetIndex.BuildIndex());
            menu.AppendAction($"Clear Index Cache", a =>
            {
                bool dialogResult = EditorUtility.DisplayDialog("Clear Index Cache", "Are you sure you want to clear the index?", "Yes", "Cancel");
                if (dialogResult)
                    AssetIndex.ClearIndex();
            });
            //---------------------------------------------------------------
            menu.AppendSeparator();
            //---------------------------------------------------------------

            string lastIndex = AssetIndex.IsIndexed ?
                (AssetIndex.LastIndexSpan.TotalHours > 24 ?
                AssetIndex.LastIndexTime.ToShortDateString().Replace('/', '-') :
                AssetIndex.LastIndexTime.ToShortTimeString())
                : "Never";
            menu.AppendAction($"Last scan: {lastIndex}", a => { }, DropdownMenuAction.Status.Disabled);
        }
        private void CreateScanWarningMessage(Toolbar toolbar)
        {
            m_ScanWarningMessage = new VisualElement();
            toolbar.Add(m_ScanWarningMessage);

            //Style
            m_ScanWarningMessage.AddToClassList(ScanWarningPromptUssClassName);
            m_ScanWarningMessage.style.position = Position.Absolute;

            //Hide on double click
            m_ScanWarningMessage.pickingMode = PickingMode.Position;


            //Dismiss
            m_ScanWarningMessage.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount >= 2)
                {
                    ShowScanWarningPrompt(false);
                }
            });

            //Warning Image
            var warningImage = new VisualElement();
            warningImage.style.backgroundImage = UIExtensions.GetEditorIcon("Warning@2x");
            warningImage.style.height = warningImage.style.width = 16;
            m_ScanWarningMessage.Add(warningImage);

            //Text
            m_ScanWarningPromptText = new Label(SCAN_WARNING_MSG);
            m_ScanWarningMessage.Add(m_ScanWarningPromptText);

            var btn = new Button(() => AssetIndex.BuildIndex()) { text = "Scan"};
            btn.AddToClassList("smallButton");
            m_ScanWarningMessage.Add(btn);
        }

        
        
        
        


        internal void ShowScanWarningPrompt(bool enabled, string custom_message = null)
        {
            m_ScanWarningMessage.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            m_ScanWarningPromptText.text = custom_message??SCAN_WARNING_MSG;

            if (enabled)
                m_ScanToolbarDropdown.AddToClassList("scan-warn-button");
            else
                m_ScanToolbarDropdown.RemoveFromClassList("scan-warn-button");
        }
    }
}