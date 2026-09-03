using System;
using UnityEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Farbod.Prefabbricato.Backend;
using static System.Net.Mime.MediaTypeNames;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// The Element used to display a set of Prefabs.
    /// You can multi-select Prefabs in this view to perform batch operations.
    /// </summary>
#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class PrefabbricatoLibraryView : VisualElement
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
        private Tab m_ProjectDirTab;
        internal LibraryLabelsView LibraryLabelsView { get; private set; }


        private DropdownMenu m_ToolbarDropdown;


        private VisualElement m_ScanWarningPrompt;
#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif


        public PrefabbricatoLibraryView()
        {
            PopulateElement();

            SetScanWarningPromptEnabled(true);

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

            //Toolbar dropdown menu
            var toolbarMenu = new ToolbarMenu();
            toolbarMenu.
                Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage =
                new StyleBackground((Texture2D)EditorGUIUtility.IconContent("_Menu@2x").image);
            m_ToolbarDropdown = toolbarMenu.menu;
            CraeteToolbarMenuOptions(m_ToolbarDropdown);
            toolbar.Add(toolbarMenu);

            #region scan needed prompt
            //Scan warning prompt (shows up next to tool menu to point at it)
            m_ScanWarningPrompt = new VisualElement();
            toolbar.Add(m_ScanWarningPrompt);
            m_ScanWarningPrompt.AddToClassList(ScanWarningPromptUssClassName);
            //Position
            m_ScanWarningPrompt.style.position = Position.Absolute;
            m_ScanWarningPrompt.style.left = new(new Length(100, LengthUnit.Percent));
            
            //Warning Image
            var warningImage = new VisualElement();
            warningImage.style.backgroundImage = new StyleBackground((Texture2D)EditorGUIUtility.IconContent("Warning@2x").image);
            warningImage.style.height = warningImage.style.width = 16;
            m_ScanWarningPrompt.Add(warningImage);
            //Text
            var warningLabel = new Label(SCAN_WARNING_MSG);
            m_ScanWarningPrompt.Add(warningLabel);
            #endregion

            ///----------------------------------------------
            ///----------------  Tab View  ------------------
            m_TabView = new();
            m_TabView.style.flexGrow = 1;
            m_TabView.contentContainer.style.flexGrow = 1;
            hierarchy.Add(m_TabView);

            //Project tab
            m_ProjectDirTab = AddTab(TAB_PROJECT_FILES_TITLE, projectTabUssClassName);
            PopulateProjectTab();

            //Asset labels tab
            var assetLabelsTab = AddTab(TAB_ASSET_LABELS_TITLE, projectTabUssClassName);
            LibraryLabelsView = new();
            assetLabelsTab.Add(LibraryLabelsView);

            m_TabView.MakeHeaderStyleButtonGroup();
        }

        private void CraeteToolbarMenuOptions(DropdownMenu menu)
        {
            menu.AppendAction("Build Index (Scan)", a => AssetIndex.BuildIndex());
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
            var tab  = new Tab(title);
            tab.AddToClassList(ussClassName);

            //Setting flex frow to 1 so the tab fill the entire panel
            tab.style.flexGrow = 1;
            tab.contentContainer.style.flexGrow = 1;

           
            m_TabView.Add(tab);
            return tab;
        }

        void PopulateProjectTab()
        {
            var content = m_ProjectDirTab.contentContainer;
            content.Add(new Label("Directory"));
        }
        


        internal void SetScanWarningPromptEnabled(bool enabled)
        {
            m_ScanWarningPrompt.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}