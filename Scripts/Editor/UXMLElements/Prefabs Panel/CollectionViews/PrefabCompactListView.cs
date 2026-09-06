using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.Linq;
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
        internal readonly static string ussClassName = "prefab-compact-list-view";
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;
        private static readonly Background s_PrefabIcon = UIExtensions.GetEditorIcon("Prefab Icon");

        private readonly MultiColumnListView m_ListView;
        private List<PrefabData> m_Data = new();

        public VisualElement Self => this;

        public event Action<IReadOnlyList<PrefabData>> selectionChanged;
        public event Action<PrefabData> itemDoubleClicked;
        public event Action<string> assetLabelClicked;
        public event Action<string, ContextualMenuPopulateEvent> labelContextMenu;

        internal PrefabCompactListView()
        {
            AddToClassList(ussClassName);
            style.flexGrow = 1;

            var columns = new Columns
            {
                new Column { name = "name", title = "Name", optional = false, width = 220, makeCell = MakeNameCell, bindCell = BindNameCell},
                new Column { name = "labels", title = "Asset Labels", width = 220, makeCell = MakeLabelsCell, bindCell = BindLabelsCell },
                new Column { name = "path", title = "Path", stretchable = true, makeCell = MakePathCell, bindCell = BindPathCell },
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
                    LabelUtilities.GetLabelColor(labelName)
                    );
                label.onClick += assetLabelClicked;
                label.onContextMenu += labelContextMenu;
                
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