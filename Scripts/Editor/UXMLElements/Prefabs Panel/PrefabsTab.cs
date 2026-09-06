using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// The Element used to display a set of Prefabs.
    /// You can multi-select Prefabs in this view to perform batch operations.
    /// </summary>
    internal class PrefabsTab : VisualElement
    {

        //Config
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/PrefabbricatoPrefabViewStyle";
        static readonly string EMPTY_LABEL_MESSAGE = "List is empty";
        //USS
        internal readonly static string ussClassName = PrefabPanelView.ussClassName+"_tab";
        internal readonly static string emptyLabelUssClassName = PrefabPanelView.ussClassName + "__empty-label";
        //Elements
        ScrollView m_Scroll;
        Label m_EmptyLabel;
        private DropdownMenu m_ToolbarDropdown;
        private ToolbarSearchField m_SearchField;
        public override VisualElement contentContainer => m_Scroll.contentContainer;

        //Script
        private List<PrefabData> m_Data = new();
        internal List<PrefabData> Data
        {
            get => m_Data;
            set {
                m_Data= value??new();
                Refresh();
            }
        }

        internal PrefabsTab(Tab tab, Func<string, PrefabData> onSearch)
        {
            name = ussClassName;
            AddToClassList(ussClassName);
            PopulateElement();

            Refresh();
        }
        private void PopulateElement()
        {
            //Header Toolbar
            CreateToolbar();

            //Scroll view
            m_Scroll = new()
            {
                mode = ScrollViewMode.Vertical,
                verticalPageSize = 400,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                style =
                {
                    flexGrow = 1,
                }
            };
            hierarchy.Add(m_Scroll);
        }
        private void CreateToolbar()
        {
            var toolbar = new Toolbar();
            
            //Toolbar dropdown menu
            var toolbarMenu = new ToolbarMenu();
            toolbarMenu.
                Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage =
                UIExtensions.GetEditorIcon("_Menu@2x");
            m_ToolbarDropdown = toolbarMenu.menu;
            
            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;
            
            //Search bar
            m_SearchField = new ToolbarSearchField();

            hierarchy.Add(toolbar);
            toolbar.Add(toolbarMenu);
            toolbar.Add(toolbar_space);
            toolbar.Add(m_SearchField);
        }

        private void Refresh()
        {
            contentContainer.Clear();

            foreach (var item in m_Data)
            {
                Add(BuildAndBind(item));
            }
            UpdateScrollViewLabel();
        }

        private VisualElement BuildAndBind(PrefabData item)
        {
            var entry = new PrefabEntryItem();
            entry.Bind(item);
            return entry;
        }

        internal void UpdateScrollViewLabel()
        {
            bool flag = m_Data.Count == 0;

            if (flag)
            {
                if (m_EmptyLabel == null)
                {
                    m_EmptyLabel = new(EMPTY_LABEL_MESSAGE);
                    Add(m_EmptyLabel);
                }
                    

                m_EmptyLabel.EnableInClassList(emptyLabelUssClassName, flag);
            }
            else
            {
                m_EmptyLabel?.RemoveFromHierarchy();
                m_EmptyLabel = null;
            }
        }

    }



}