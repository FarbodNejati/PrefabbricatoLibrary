using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// The Element used to display a set of Prefabs.
    /// You can multi-select Prefabs in this view to perform batch operations.
    /// </summary>
#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class PrefabListView : VisualElement
    {
        //Config
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/PrefabbricatoPrefabViewStyle";

        //USS
        internal readonly static string ussClassName = "prefab-view";


        ScrollView m_Scroll;
        private DropdownMenu m_ToolbarDropdown;
        private ToolbarSearchField m_SearchField;


#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif
        public override VisualElement contentContainer => m_Scroll.contentContainer;

        public PrefabListView()
        {
            PopulateElement();

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

            //Toolbar dropdown menu
            var toolbarMenu = new ToolbarMenu();
            toolbarMenu.
                Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage =
                UIExtensions.GetEditorIcon("_Menu@2x");
            m_ToolbarDropdown = toolbarMenu.menu;
            toolbar.Add(toolbarMenu);

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;
            toolbar.Add(toolbar_space);

            //Search bar
            m_SearchField = new ToolbarSearchField();
            toolbar.Add(m_SearchField);

            //Scroll view
            m_Scroll = new();
            hierarchy.Add(m_Scroll);
        }
    }
}