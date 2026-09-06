using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    internal class PrefabEntryItem : VisualElement
    {
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;


        internal readonly static string ussClassName = "prefab-entry";
        private readonly static string m_PreviewImageUssClassName = m_PreviewImageUssClassName + "_content__image";
        private readonly static string m_NameLabelUssClassName = m_PreviewImageUssClassName + "_content__name";
        private readonly static string m_LabelsContainerUssClassName = m_PreviewImageUssClassName + "_content__labels";

        private readonly static string m_AssetLabelUssClassName = "asset-label";
        private static Background m_LabelIconImage = UIExtensions.GetEditorIcon("FilterByLabel");

        private static readonly Dictionary<string, Texture2D> s_PreviewCache = new();
        private IVisualElementScheduledItem m_PreviewPoll;

        internal event Action<string> doubleClicked;
        internal event Action<string> labelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;

        private readonly Image m_PreviewImage;
        private readonly Label m_NameLabel;
        private readonly VisualElement m_LabelsContainer;

        private string m_Guid;

        internal PrefabEntryItem()
        {
            AddToClassList(ussClassName);

            m_PreviewImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            m_PreviewImage.AddToClassList(m_PreviewImageUssClassName);
            Add(m_PreviewImage);

            m_NameLabel = new Label();
            m_NameLabel.AddToClassList(m_NameLabelUssClassName);
            Add(m_NameLabel);

            m_LabelsContainer = new VisualElement();
            m_LabelsContainer.AddToClassList(m_LabelsContainerUssClassName);
            m_LabelsContainer.style.flexDirection = FlexDirection.Row;
            m_LabelsContainer.style.flexWrap = Wrap.Wrap;
            Add(m_LabelsContainer);

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                    doubleClicked?.Invoke(m_Guid);
            });
        }

        /// <summary>
        /// Populates this element with the given data. Does not mutate the data object.
        /// </summary>
        internal void Bind(PrefabData data)
        {
            if (data.prefab == null)
            {
                Unbind();
                return;
            }

            m_Guid = data.guid;
            m_NameLabel.text = data.name;

            m_PreviewPoll?.Pause();
            m_PreviewPoll = null;
            SetPreview(data);

            m_LabelsContainer.Clear();
            foreach (var labelName in data.labels)
            {
                m_LabelsContainer.Add(CreateLabel(labelName, GetAssetLabelColor(labelName)));
            }
        }
        private void SetPreview(PrefabData data)
        {
            if (s_PreviewCache.TryGetValue(data.guid, out var cached) && cached != null)
            {
                m_PreviewImage.image = cached;
                return;
            }

            if (data.prefab == null)
            {
                m_PreviewImage.image = null;
                return;
            }

            // Immediate placeholder — cheap and synchronous.
            m_PreviewImage.image = AssetPreview.GetMiniThumbnail(data.prefab);

            string guid = data.guid; // capture for closure safety against rebinds
            int attempts = 0;
            const int maxAttempts = 100; // ~5s at 50ms

            m_PreviewPoll = schedule.Execute(() =>
            {
                // Bail out if this element has been rebound to a different item since we started.
                if (m_Guid != guid) { m_PreviewPoll?.Pause(); return; }

                var tex = AssetPreview.GetAssetPreview(data.prefab);
                if (tex != null)
                {
                    s_PreviewCache[guid] = tex;
                    m_PreviewImage.image = tex;
                    m_PreviewPoll?.Pause();
                    return;
                }

                if (++attempts >= maxAttempts)
                {
                    // Give up gracefully, keep the mini thumbnail.
                    m_PreviewPoll?.Pause();
                }
            }).Every(50);
        }

        private Color GetAssetLabelColor(string labelName)
        {
            return LabelUtilities.GetLabelColor(labelName, TAG_COLOR_DEFAULT);
        }
        internal void Unbind()
        {
            m_Guid = null;
            m_NameLabel.text = string.Empty;
            m_PreviewImage.image = null;
            m_LabelsContainer.Clear();
        }

        private VisualElement CreateLabel(string labelName, Color color)
        {
            #region template
            var container = new VisualElement();

            var ve = new VisualElement();
            container.Add(ve);
            ve.AddToClassList(m_AssetLabelUssClassName);

            //Label icon
            var icon = new VisualElement();
            icon.style.width = icon.style.height = 12;
            icon.style.backgroundImage = m_LabelIconImage;
            ve.Add(icon);

            //Label name
            var nameLabel = new Label("label");
            ve.Add(nameLabel);
            #endregion

            #region events
            //Click event
            ve.RegisterCallback<ClickEvent>(evt =>
            {
                labelClicked?.Invoke(labelName);
            });
            //Context menu manipulator
            ve.AddManipulator(new ContextualMenuManipulator(e =>
            {
                onLabelContextMenu?.Invoke(labelName, e);
            }));
            #endregion

            nameLabel.text = labelName;
            ve.style.backgroundColor = color;

            return container;
        }
    }
}

