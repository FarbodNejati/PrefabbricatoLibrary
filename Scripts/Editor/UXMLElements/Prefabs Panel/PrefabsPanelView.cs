using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.SceneManagement;
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
    public partial class PrefabPanelView : VisualElement
    {
#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif

        //Config
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/PrefabViewStyle";

        //USS
        internal readonly static string ussClassName = "prefabs-view";
        internal readonly static string tabViewUssClassName = ussClassName + "_tab-view";
        internal readonly static string addTabButtonUssClassName = tabViewUssClassName + "_add-button";

        //The tab view which holds our PrefabsTabs
        private TabView m_TabView;

        public override VisualElement contentContainer => m_TabView.contentContainer;

        internal List<PrefabsTab> allTabs { get; private set; } = new();
        internal PrefabsTab activeTab { get; private set; }
        internal event Action<PrefabsTab> activeTabChanged;
        internal event Action<IReadOnlyList<PrefabData>> selectionChanged;
        internal event Action<DropdownMenu, IReadOnlyList<PrefabData>> assetsContextMenu;
        internal int activeTabIndex
        {
            get => m_TabView.selectedTabIndex;
            set => m_TabView.selectedTabIndex = value;
        }
        public PrefabPanelView()
        {
            //Load default stylesheet
            StyleSheet style = Resources.Load<StyleSheet>(STYLESHEET_RESOURCE_PATH);
            Debug.Assert(style != null, $"[{this.GetType().Name}] Stylesheet not found at {STYLESHEET_RESOURCE_PATH}");
            styleSheets.Add(style);

            PopulateElement();

            CreateTab();
        }

        private void PopulateElement()
        {
            AddToClassList(ussClassName);

            //Tab view
            m_TabView = new TabView() { 
                name = tabViewUssClassName,
                reorderable = true,
                style =
                {
                    flexGrow = 1,
                }
            };
            m_TabView.Query<RepeatButton>().ForEach(button =>
            {
                button.AddToClassList("tab-header-controls");
            });
            m_TabView.AddToClassList(tabViewUssClassName);
            m_TabView.activeTabChanged += OnActiveTabChanged;
            m_TabView.contentContainer.style.flexGrow = 1;


            //Add tab button
            var addButton = new Button(()=>CreateTab()) {
                name = addTabButtonUssClassName,
                iconImage = UIExtensions.GetEditorIcon("CreateAddNew@2x"),
                tooltip = "New Tab",
                style =
                {
                    position = Position.Absolute,
                    right = 0,
                    marginRight = 4,
                    width=16,
                    height=16,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2,
                    paddingTop = 2,
                }
            };
            addButton.AddToClassList(addTabButtonUssClassName);
            addButton.AddToClassList("tab-header-controls");
            var headerViewport = m_TabView.Q<VisualElement>(className: TabView.viewportUssClassName);
            m_TabView.contentViewport.Add(addButton);

            //Fadeout
            var fadeout = new VisualElement();
            fadeout.pickingMode = PickingMode.Ignore;
            fadeout.AddToClassList("fadeout");
            headerViewport.Insert(1, fadeout);



            //m_TabView.contentViewport
            hierarchy.Add(m_TabView);
        }
        internal PrefabsTab CreateTab(Query initialQuery = null)
        {
            Tab tab = new Tab() {
                closeable = true,
                label = "New Tab",
                style =
                {
                    flexGrow = 1,
                }
            };
            tab.contentContainer.style.flexGrow = 1;
            PrefabsTab prefabsTab = new PrefabsTab(tab, initialQuery);
            prefabsTab.style.flexGrow = 1;
            


            #region tab-seperator
            //This does not appear without custom styling
            VisualElement tabSeparator = new()
            {
                name = Tab.ussClassName + "__separator",
                style =
                {
                    position= Position.Absolute,
                    right = -1.1f,
                    width=0.5f,
                    height=10,
                    alignSelf=Align.Center,
                }
            };
            tab.tabHeader.Add(tabSeparator);
            #endregion


            //Context menu manipulator
            tab.tabHeader.AddManipulator(new ContextualMenuManipulator(e =>
            {
                // Check if the tab is still a child of the TabView
                if (!m_TabView.contentContainer.Contains(tab))
                    return;

                //int index = headerContainer.IndexOf(tab.tabHeader);
                //var tab = m_TabView.GetTab(index);

                if (tab != null)
                    BuildTabHeaderContextMenu(e.menu, tab);
            }));

            prefabsTab.onAssetsContextMenu += (e, a) =>
            {
                assetsContextMenu?.Invoke(e.menu, a);
            };

            allTabs.Add(prefabsTab);
            tab.closed += t => OnTabRemoved(t, prefabsTab);
            tab.Add(prefabsTab);
            m_TabView.Add(tab);

            int tabCount = m_TabView.childCount;

            m_TabView.selectedTabIndex = tabCount-1;
            OnActiveTabChanged(null, tab);
            return prefabsTab;
        }
        private void OnActiveTabChanged(Tab oldTab, Tab newTab)
        {
            if (activeTab != null)
                activeTab.onSelectionChanged -= selectionChanged;
            activeTab = newTab.Q<PrefabsTab>();

            activeTabChanged?.Invoke(activeTab);
            activeTab.onSelectionChanged += i => {
                selectionChanged?.Invoke(i);
            };
        }

        private void OnTabRemoved(Tab tab, PrefabsTab prefabTab)
        {
            allTabs.Remove(prefabTab);
        }

        /// <summary>
        /// Returns the active tab, or creates a new one if needed.
        /// </summary>
        internal PrefabsTab ActiveOrNewTab()
        {
            if(m_TabView.activeTab == null || activeTab == null)
                CreateTab();

            return activeTab;
        }
        private void BuildTabHeaderContextMenu(DropdownMenu menu, Tab tab)
        {
            menu.AppendAction("Close", e => { tab.RemoveFromHierarchy(); });
            menu.AppendSeparator();
            menu.AppendAction("Close All", e => m_TabView.Clear());
            menu.AppendAction("Close All But This", e => m_TabView.Query<Tab>().Where(t => t != tab).ForEach(x => x.RemoveFromHierarchy()));
            menu.AppendAction("Close All On Right", e =>
            {
                int thisIndex = m_TabView.contentContainer.IndexOf(tab);
                if (thisIndex == -1) return;

                var tabs = m_TabView.Query<Tab>().ToList();
                tabs.Where((t, index) => index > thisIndex).ToList().ForEach(t => t.RemoveFromHierarchy());
            });
            menu.AppendAction("Close All On left", e =>
            {
                int thisIndex = m_TabView.contentContainer.IndexOf(tab);
                if (thisIndex == -1) return;

                var tabs = m_TabView.Query<Tab>().ToList();
                tabs.Where((t, index) => index < thisIndex).ToList().ForEach(t => t.RemoveFromHierarchy());
            });
        }

        
        internal void ClearTabs()
        {
            m_TabView.Clear();
            allTabs.Clear();
        }
    }
}