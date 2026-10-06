using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// Used for saving and restoring window state between sessions
    /// </summary>
    [Serializable]
    internal class SerializableQueryData
    {
        public string SearchText;
        public List<string> Labels;
        public LabelQueryMode LabelMode;
        public List<string> Paths;
        public PathMatchMode PathMode;
        public QueryResult.QuerySortMode SortMode;

        public SerializableQueryData(Query query) { 
            SearchText = query.SearchText; 
            Labels = query.Labels.Labels.ToList(); 
            LabelMode = query.Labels.Mode; 
            Paths = query.Paths.Paths.ToList(); 
            PathMode = query.Paths.Mode; SortMode = query.SortMode; 
        }
        public Query ToQuery() { 
            Query query = new Query(); 
            ApplyTo(query); 
            return query; 
        }
        public void ApplyTo(Query query) { 
            query.Reset(); 
            query.SetText(SearchText); 
            query.SetLabels(Labels ?? Enumerable.Empty<string>(), LabelMode); 
            query.SetPaths(Paths ?? Enumerable.Empty<string>(), PathMode); 
            query.SetSortMode(SortMode); 
        }
    }


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

        internal event Action<Query> onRefresh;
        internal event Action<IReadOnlyList<PrefabData>> onSelectionChanged;
        internal event Action<PrefabData> onItemDoubleClicked;
        internal event Action<string> onLabelClicked;
        internal event Action<ContextualMenuPopulateEvent, IReadOnlyList<PrefabData>> onAssetsContextMenu;


        /// <summary>
        /// The query currently being used to generate the displayed data.
        /// Null means that no filtering query is active.
        /// </summary>
        internal readonly Query ActiveQuery;

        /// <summary>
        /// The evaluated result of the active query.
        /// Kept so it can be re-evaluated and sorted without creating
        /// another query object.
        /// </summary>
        private QueryResult m_QueryResult;
        private Tab m_TabElement;
        private List<PrefabData> m_Data = new();

        internal List<PrefabData> Data
        {
            get => m_Data;

            set
            {
                m_Data = value ?? new();
                RefreshView();
            }
        }

        internal PrefabsTab(Tab tab, Query query = null)
        {
            m_TabElement = tab;
            //Initialize query object
            ActiveQuery = query ?? new();

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
            m_ViewModeMenu = new ToolbarMenu() { tooltip = "View mode" }.WithIcon("d_ListView@2x");
            BuildViewModeMenu();

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;

            //Search bar
            m_SearchField = new ToolbarSearchField();
            m_SearchField.RegisterValueChangedCallback(SearchFieldUpdated);

            hierarchy.Add(toolbar);
            toolbar.Add(toolbarMenu);
            toolbar.Add(m_ViewModeMenu);
            toolbar.Add(toolbar_space);
            toolbar.Add(m_SearchField);
        }

        private void SearchFieldUpdated(ChangeEvent<string> evt)
        {
            ActiveQuery.SetText(evt.newValue); //Update search text
            ActiveQuery.SetLabels(Array.Empty<string>()); //Reset label filters
            Refresh();
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

            view.selectionChanged += items =>
            {
                onSelectionChanged?.Invoke(items);
            };
            view.itemDoubleClicked += data => onItemDoubleClicked?.Invoke(data);
            view.assetLabelClicked += name => onLabelClicked?.Invoke(name);
            view.buildAssetContextMenu += (evt, assets) => onAssetsContextMenu?.Invoke(evt, assets);

            m_Views[mode] = view;
            return view;
        }
        private void BuildViewModeMenu()
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
        private void RefreshView()
        {
            m_ActiveView?.SetData(m_Data);
            onRefresh?.Invoke(ActiveQuery);
        }

        public void RemoveTab()
        {
            m_TabElement.RemoveFromHierarchy();
        }
        /// <summary>
        /// Sets the active user query and immediately evaluates it.
        /// Passing null or an empty string clears the query and displays
        /// the complete asset database.
        /// </summary>
        //internal void SetQuery(string query)
        //{
        //    query ??= string.Empty;

        //    if (string.IsNullOrWhiteSpace(query))
        //    {
        //        m_ActiveQuery.Reset();
        //        m_QueryResult = null;

        //        m_Data = AssetIndex.PrefabDataList.ToList();

        //        RefreshView();
        //        return;
        //    }
        //    if(m_ActiveQuery==null)
        //        m_ActiveQuery = new Query();

        //    m_ActiveQuery.Reset();
        //    m_ActiveQuery.SearchText = query;
        //    m_QueryResult = m_ActiveQuery.Evaluate();

        //    m_Data = m_QueryResult.Results.ToList();

        //    RefreshView();
        //}

        /// <summary>
        /// Sets an arbitrary query as the active query and immediately
        /// evaluates it.
        ///
        /// This allows external systems to use queries other than
        /// UserQuery, such as LabelQuery or PathQuery.
        /// </summary>
        //internal void SetQuery(Query query)
        //{
        //    m_ActiveQuery = query;

        //    if (query == null)
        //    {
        //        m_QueryResult = null;
        //        m_Data = AssetIndex.PrefabDataList.ToList();
        //    }
        //    else
        //    {
        //        m_QueryResult = query.Evaluate();
        //        m_Data = m_QueryResult.Results.ToList();
        //    }

        //    RefreshView();
        //}

        /// <summary>
        /// Re-evaluates the currently active query.
        ///
        /// If no query is active, the complete asset database is displayed.
        /// </summary>
        internal void Refresh()
        {
            if (ActiveQuery == null)
            {
                m_Data = null;
            }
            else
            {
                if (m_QueryResult == null)
                    m_QueryResult = ActiveQuery.Evaluate();
                else
                    m_QueryResult.ReEvaluate();

                m_Data = m_QueryResult.Results.ToList();
            }

            m_TabElement.label = ActiveQuery.ToString();
            RefreshView();
        }
    }



}