using System;
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
    public partial class PrefabbricatoPrefabListView : VisualElement
    {
        //Config
        static readonly string STYLESHEET_RESOURCE_PATH = "Style/PrefabbricatoPrefabViewStyle";
        
        //USS
        internal readonly static string ussClassName = "prefab-view";


        ScrollView m_Scroll;

#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif

        public override VisualElement contentContainer => m_Scroll.contentContainer;

        public PrefabbricatoPrefabListView()
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

            //Scroll view
            m_Scroll = new();
            hierarchy.Add(m_Scroll);
        }
    }
}