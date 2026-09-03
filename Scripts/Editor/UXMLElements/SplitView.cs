using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;
using Cursor = UnityEngine.UIElements.Cursor;
using NUnit.Framework.Constraints;

namespace Farbod.Prefabbricato
{


#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    partial class SplitView : TwoPaneSplitView
    {
        #if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<SplitView, UxmlTraits> {}
        public new class UxmlTraits : TwoPaneSplitView.UxmlTraits{}
#endif
    }
}