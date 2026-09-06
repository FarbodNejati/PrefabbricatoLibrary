using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// Pure selection-state logic (guid-based), used only by views that don't get native
    /// selection from a built-in collection view (i.e. the grid views). Handles the standard
    /// single / ctrl-toggle / shift-range click semantics.
    /// </summary>
    internal class PrefabSelectionController
    {
        private readonly HashSet<string> m_Selected = new();
        private int m_LastClickedIndex = -1;

        internal event Action selectionChanged;
        internal bool IsSelected(string guid) => guid != null && m_Selected.Contains(guid);

        internal void Clear()
        {
            if (m_Selected.Count == 0) return;
            m_Selected.Clear();
            m_LastClickedIndex = -1;
            selectionChanged?.Invoke();
        }

        internal void HandleClick(int index, string guid, IReadOnlyList<PrefabData> source, bool ctrl, bool shift)
        {
            if (shift && m_LastClickedIndex >= 0 && m_LastClickedIndex < source.Count)
            {
                if (!ctrl) m_Selected.Clear();

                int from = Mathf.Min(m_LastClickedIndex, index);
                int to = Mathf.Max(m_LastClickedIndex, index);
                for (int i = from; i <= to; i++)
                    m_Selected.Add(source[i].guid);
            }
            else if (ctrl)
            {
                if (!m_Selected.Add(guid))
                    m_Selected.Remove(guid);
                m_LastClickedIndex = index;
            }
            else
            {
                m_Selected.Clear();
                m_Selected.Add(guid);
                m_LastClickedIndex = index;
            }

            selectionChanged?.Invoke();
        }
    }
}