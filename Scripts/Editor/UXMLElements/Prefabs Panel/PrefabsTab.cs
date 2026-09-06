using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    internal enum PrefabViewMode
    {
        CompactList,
        List,
        CompactGrid,
        Grid
    }

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
        internal readonly static string ussClassName = PrefabPanelView.ussClassName + "_tab";
        internal readonly static string emptyLabelUssClassName = PrefabPanelView.ussClassName + "__empty-label";
        //Elements

        private VisualElement m_ViewContainer;
        private Label m_EmptyLabel;
        private ToolbarMenu m_ViewModeMenu;
        private ToolbarSearchField m_SearchField;
        private readonly Dictionary<PrefabViewMode, IPrefabCollectionView> m_Views = new();

        //Script
        private PrefabViewMode m_ViewMode = PrefabViewMode.CompactList;
        private IPrefabCollectionView m_ActiveView;


        internal event Action<IReadOnlyList<PrefabData>> selectionChanged;
        internal event Action<PrefabData> itemDoubleClicked;
        internal event Action<string> labelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> labelContextMenu;


        private List<PrefabData> m_Data = new();
        internal List<PrefabData> Data
        {
            get => m_Data;
            set { m_Data = value ?? new(); Refresh(); }
        }

        internal PrefabsTab(Tab tab, Func<string, PrefabData> onSearch)
        {
            name = ussClassName;
            AddToClassList(ussClassName);
            PopulateElement();

            SetViewMode(m_ViewMode);
            Refresh();
        }
        private void PopulateElement()
        {
            CreateToolbar();
            m_ViewContainer = new VisualElement { style = { flexGrow = 1 } };
            hierarchy.Add(m_ViewContainer);
        }
        private void CreateToolbar()
        {
            var toolbar = new Toolbar();

            //Toolbar dropdown menu
            var toolbarMenu = new ToolbarMenu() { tooltip = "Options" }.WithIcon("_Menu@2x");
            toolbarMenu.SetEnabled(false);

            //Viewmode menu
            m_ViewModeMenu = new ToolbarMenu() { tooltip= "View mode" }.WithIcon("d_ListView@2x");
            BuildViewModeMenu();

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;

            //Search bar
            m_SearchField = new ToolbarSearchField();

            hierarchy.Add(toolbar);
            toolbar.Add(toolbarMenu);
            toolbar.Add(m_ViewModeMenu);
            toolbar.Add(toolbar_space);
            toolbar.Add(m_SearchField);
        }

        private void SetViewMode(PrefabViewMode mode)
        {
            m_ViewMode = mode;
            //m_ViewModeMenu.text = GetViewModeLabel(mode);
            BuildViewModeMenu();

            if (m_ActiveView != null)
                m_ViewContainer.Remove(m_ActiveView.Self);

            m_ActiveView = GetOrCreateView(mode);
            m_ViewContainer.Add(m_ActiveView.Self);
            m_ActiveView.SetData(m_Data);
        }

        private IPrefabCollectionView GetOrCreateView(PrefabViewMode mode)
        {
            if (m_Views.TryGetValue(mode, out var existing))
                return existing;

            IPrefabCollectionView view = mode switch
            {
                //PrefabViewMode.CompactList => new PrefabCompactListView(),
                //PrefabViewMode.List => new PrefabListView(),
                //PrefabViewMode.CompactGrid => new PrefabCompactGridView(),
                //PrefabViewMode.Grid => new PrefabGridView(),
                _ => new PrefabCompactListView()
            };

            view.selectionChanged += items => selectionChanged?.Invoke(items);
            view.itemDoubleClicked += data => itemDoubleClicked?.Invoke(data);
            view.assetLabelClicked += name => labelClicked?.Invoke(name);
            view.labelContextMenu += (name, evt) => labelContextMenu?.Invoke(name, evt);

            m_Views[mode] = view;
            return view;
        }
        void BuildViewModeMenu()
        {
            m_ViewModeMenu.menu.ClearItems();
            foreach (PrefabViewMode mode in Enum.GetValues(typeof(PrefabViewMode)))
            {
                var name = System.Text.RegularExpressions.Regex.Replace(
                    mode.ToString(),
                    "([a-z])([A-Z])",
                    "$1 $2");
                m_ViewModeMenu.menu.AppendAction(
                    name,
                    _ => SetViewMode(mode),
                    _ => m_ViewMode == mode ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
        }
        private void Refresh()
        {
            m_ActiveView?.SetData(m_Data);
        }

    }



}