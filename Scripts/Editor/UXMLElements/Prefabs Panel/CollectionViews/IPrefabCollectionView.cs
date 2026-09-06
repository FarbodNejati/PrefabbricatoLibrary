using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// Common surface every prefab collection view exposes to PrefabsTab.
    /// </summary>
    internal interface IPrefabCollectionView
    {
        VisualElement Self { get; }
        void SetData(List<PrefabData> data);

        event Action<IReadOnlyList<PrefabData>> selectionChanged;
        event Action<PrefabData> itemDoubleClicked;
        event Action<string> assetLabelClicked;
        event Action<string, ContextualMenuPopulateEvent> labelContextMenu;
    }
}