using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// Dense, table view: name, labels, asset path.
    /// Backed entirely by MultiColumnListView
    /// 
    /// <para>Unity 2022.2+ Required</para>
    /// </summary>
    internal class PrefabCompactListView : VisualElement, IPrefabCollectionView
    {
        //Config
        private const int DRAG_THRESHOLD = 12;
        internal const string ussClassName = "prefab-compact-list-view";
        private static readonly Background s_PrefabIcon = UIExtensions.GetEditorIcon("Prefab Icon");



        private readonly MultiColumnListView m_ListView;
        private List<PrefabData> m_Data = new();

        public VisualElement Self => this;

        public event Action<IReadOnlyList<PrefabData>> selectionChanged;
        public event Action<IReadOnlyList<PrefabData>> assetDragStarted;
        public event Action<PrefabData> itemDoubleClicked;
        public event Action<string> assetLabelClicked;
        public event Action<ContextualMenuPopulateEvent, IReadOnlyList<PrefabData>> buildAssetContextMenu;

        internal PrefabCompactListView()
        {
            AddToClassList(ussClassName);
            style.flexGrow = 1;

            var columns = new Columns
            {
                new Column {
                    name = "name", title = "Name",
                    optional = false,
                    stretchable = true, width = 220, minWidth = 80, 
                    makeCell = MakeNameCell, bindCell = BindNameCell},
                
                new Column { 
                    name = "labels", title = "Asset Labels", 
                    icon = UIExtensions.GetEditorIcon("FilterByLabel@2x"),
                    stretchable = true, width = 220, minWidth = 110, 
                    makeCell = MakeLabelsCell, bindCell = BindLabelsCell },
                
                new Column { 
                    name = "path", title = "Path",
                    stretchable = true, minWidth = 40, 
                    makeCell = MakePathCell, bindCell = BindPathCell },
            };

            m_ListView = new MultiColumnListView(columns)
            {
                itemsSource = m_Data,
                selectionType = SelectionType.Multiple,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = { flexGrow = 1 }
            };

            m_ListView.itemsChosen += items => itemDoubleClicked?.Invoke(items.Cast<PrefabData>().FirstOrDefault());
            m_ListView.selectionChanged += items => selectionChanged?.Invoke(items.Cast<PrefabData>().ToList());
            Add(m_ListView);

            SetupDragEvents();
        }

        internal List<PrefabData> Selection => m_ListView.selectedItems?.Select(d => (PrefabData)d).ToList();
        private Vector2 drag_start_pos;
        private bool m_Dragging = false;
        private List<PrefabData> m_DragItems = null;

        bool m_MouseDown = false;
        private void SetupDragEvents()
        {
            m_ListView.AddManipulator(new ContextualMenuManipulator(e =>
            {
                var selection = Selection;
                if (selection?.Count() > 0)
                    buildAssetContextMenu?.Invoke(e, selection);
            }));
            //Register drag events on the list view
            m_ListView.RegisterCallback<MouseDownEvent>(evt =>
            {
                 //Right Click
                if (evt.button == 1)
                    return;

                m_DragItems?.Clear();
                m_MouseDown = false;

                if (Selection?.Count() > 0)
                {
                    m_DragItems = Selection.Select(d => (PrefabData)d).ToList();
                }
                if (m_DragItems?.Count() > 0)
                {
                    drag_start_pos = evt.mousePosition;
                    m_Dragging = false;
                    m_MouseDown = true;
                }
            });
            m_ListView.RegisterCallback<MouseUpEvent>(evt =>
            {
                m_MouseDown = false;
            });
            // Mouse is moving
            m_ListView.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (evt.button == 1)
                    return;

                //Check if we are not dragging
                if (m_MouseDown && !m_Dragging && (evt.mousePosition - drag_start_pos).magnitude > DRAG_THRESHOLD)
                {
                    m_Dragging = true;
                    StartDragOperation(evt);
                }
            });

            // Something is being dragged into us
            m_ListView.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.None; //Do not accept drag events
            });
        }
        private void StartDragOperation(IMouseEvent evt)
        {
            
            if (m_DragItems?.Count > 0)
            {
                m_Dragging = true;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.StartDrag("Dragging");
                DragAndDrop.objectReferences = m_DragItems.Select(d => d.prefab).ToArray();
                DragAndDrop.paths = m_DragItems.Select(d => d.assetPath).ToArray();
                assetDragStarted?.Invoke(m_DragItems);
            }
            
        }

        public void SetData(List<PrefabData> data)
        {
            m_Data = data ?? new List<PrefabData>();
            m_ListView.itemsSource = m_Data;
            m_ListView.Rebuild();
        }

        #region Name column
        private VisualElement MakeNameCell()
        {
            var row = new VisualElement { style = { 
                    flexDirection = FlexDirection.Row, 
                    alignItems = Align.Center,
                    paddingLeft = 4,
            } };


            var icon = new VisualElement();
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.backgroundImage = s_PrefabIcon;
            var label = new Label { name = "name-label" };
            row.Add(icon);
            row.Add(label);
            return row;
        }
        private void BindNameCell(VisualElement ve, int index) => ve.Q<Label>("name-label").text = m_Data[index].name;
        #endregion

        #region Labels column
        private VisualElement MakeLabelsCell() =>
            new VisualElement { style = { 
                    flexDirection = FlexDirection.Row, 
                    flexWrap = Wrap.NoWrap,
                    overflow = Overflow.Hidden
                } };

        private void BindLabelsCell(VisualElement ve, int index)
        {
            ve.Clear();
            foreach (var labelName in m_Data[index].labels)
            {
                var label = new AssetLabelElement(
                    labelName,
                    LabelUtilities.GetLabelColor(labelName),
                    hasIcon: false
                    );
                label.onClick += assetLabelClicked;

                ve.Add(label);
            }
        }
        #endregion

        #region Path column
        private VisualElement MakePathCell() => new Label();
        private void BindPathCell(VisualElement ve, int index) => ((Label)ve).text = m_Data[index].assetPath;
        #endregion
    }
}