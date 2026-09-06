using Farbod.Prefabbricato.Backend;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    /// <summary>
    /// Displays a single PrefabData: preview image, name, and (optionally) its labels.
    /// Reused/pooled by the owning view - only the presentation changes via <see cref="SetLayout"/>;
    /// content changes via <see cref="Bind"/>. Thumbnails are cached statically (generated once,
    /// ever, per guid) and only polled for while this element is actually attached to a panel.
    /// </summary>
    internal class PrefabEntryItem : VisualElement
    {
        internal enum Layout
        {
            /// Image on the left, name + wrapping labels on the right.
            List,
            /// Image on top, name below, labels below that. Big card.
            Grid,
            /// Image on top, name below. No labels. Small card.
            CompactGrid
        }

        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;

        internal readonly static string ussClassName = "prefab-entry";
        internal readonly static string selectedUssClassName = ussClassName + "--selected";
        private readonly static string m_PreviewImageUssClassName = ussClassName + "__image";
        private readonly static string m_ContentContainerUssClassName = ussClassName + "__content";
        private readonly static string m_NameLabelUssClassName = ussClassName + "__name";
        private readonly static string m_LabelsContainerUssClassName = ussClassName + "__labels";

        private static readonly Dictionary<string, Texture2D> s_PreviewCache = new();

        internal event System.Action<string> labelClicked;
        internal event System.Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;

        private readonly Image m_PreviewImage;
        private readonly VisualElement m_ContentContainer;
        private readonly Label m_NameLabel;
        private readonly VisualElement m_LabelsContainer;

        private IVisualElementScheduledItem m_PreviewPoll;
        private Layout m_Layout = Layout.Grid;

        internal PrefabData Data { get; private set; }

        internal PrefabEntryItem()
        {
            AddToClassList(ussClassName);

            m_PreviewImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            m_PreviewImage.AddToClassList(m_PreviewImageUssClassName);
            Add(m_PreviewImage);

            m_ContentContainer = new VisualElement();
            m_ContentContainer.AddToClassList(m_ContentContainerUssClassName);
            Add(m_ContentContainer);

            m_NameLabel = new Label();
            m_NameLabel.AddToClassList(m_NameLabelUssClassName);
            m_ContentContainer.Add(m_NameLabel);

            m_LabelsContainer = new VisualElement();
            m_LabelsContainer.AddToClassList(m_LabelsContainerUssClassName);
            m_LabelsContainer.style.flexDirection = FlexDirection.Row;
            m_LabelsContainer.style.flexWrap = Wrap.Wrap;
            m_ContentContainer.Add(m_LabelsContainer);

            RegisterCallback<AttachToPanelEvent>(_ => TryStartPreview());
            RegisterCallback<DetachFromPanelEvent>(_ => StopPreviewPoll());

            ApplyLayout();
        }

        /// <summary>Switches between the list/grid/compact-grid presentations. Cheap - just re-applies styles.</summary>
        internal void SetLayout(Layout layout)
        {
            if (m_Layout == layout) return;
            m_Layout = layout;
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            EnableInClassList(ussClassName + "--list", m_Layout == Layout.List);
            EnableInClassList(ussClassName + "--grid", m_Layout == Layout.Grid);
            EnableInClassList(ussClassName + "--compact-grid", m_Layout == Layout.CompactGrid);

            style.flexDirection = m_Layout == Layout.List ? FlexDirection.Row : FlexDirection.Column;
            m_LabelsContainer.style.display = m_Layout == Layout.CompactGrid ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Populates this element with the given data. Does not mutate the data object.</summary>
        internal void Bind(PrefabData data)
        {
            Data = data;

            if (data?.prefab == null)
            {
                Unbind();
                return;
            }

            m_NameLabel.text = data.name;

            m_LabelsContainer.Clear();
            if (m_Layout != Layout.CompactGrid)
            {
                foreach (var labelName in data.labels)
                {
                    var label = new AssetLabelElement(
                        labelName,
                        LabelUtilities.GetLabelColor(labelName)
                        );
                    label.onClick += labelClicked;
                    label.onContextMenu += onLabelContextMenu;
                    m_LabelsContainer.Add(label);


                }
            }

            // Cheap placeholder immediately; the real preview arrives async, and only while attached.
            m_PreviewImage.image = s_PreviewCache.TryGetValue(data.guid, out var cached) && cached != null
                ? cached
                : AssetPreview.GetMiniThumbnail(data.prefab);

            if (panel != null)
                TryStartPreview();
        }

        internal void Unbind()
        {
            StopPreviewPoll();
            Data = null;
            m_NameLabel.text = string.Empty;
            m_PreviewImage.image = null;
            m_LabelsContainer.Clear();
        }

        private void TryStartPreview()
        {
            StopPreviewPoll();
            if (Data?.prefab == null) return;

            if (s_PreviewCache.TryGetValue(Data.guid, out var cached) && cached != null)
            {
                m_PreviewImage.image = cached;
                return;
            }

            string guid = Data.guid;
            GameObject prefab = Data.prefab;
            int attempts = 0;
            const int maxAttempts = 100; // ~5s at 50ms

            m_PreviewPoll = schedule.Execute(() =>
            {
                if (Data == null || Data.guid != guid) { StopPreviewPoll(); return; } // rebound elsewhere

                var tex = AssetPreview.GetAssetPreview(prefab);
                if (tex != null)
                {
                    s_PreviewCache[guid] = tex;
                    m_PreviewImage.image = tex;
                    StopPreviewPoll();
                    return;
                }

                if (++attempts >= maxAttempts)
                    StopPreviewPoll();
            }).Every(50);
        }

        private void StopPreviewPoll()
        {
            m_PreviewPoll?.Pause();
            m_PreviewPoll = null;
        }
    }
}