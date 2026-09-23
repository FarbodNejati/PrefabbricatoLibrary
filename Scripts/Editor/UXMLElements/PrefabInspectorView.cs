using Farbod.Prefabbricato.Backend;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{


#if UNITY_2023_2_OR_NEWER
    [UxmlElement]
#endif
    public partial class PrefabInspectorView : VisualElement
    {
        private readonly static string HEADER_TITLE = "Inspector";
        private readonly static int TAG_ADD_MAX_LENGTH = 16;

        internal readonly static string ussClassName = "inspector-view";
        private readonly static string m_contentUssClassName = ussClassName + "_content";
        private readonly static string m_contentImageUssClassName = ussClassName + "_content__image";
        private readonly static string m_contentTitleUssClassName = ussClassName + "_content__title";
        private readonly static string m_LabelContainerUssClassName = ussClassName + "_content__labels";
        private readonly static string m_LabelFieldUssClassName = ussClassName + "_content__label-field";


        internal DropdownMenu m_ToolbarDropdown;
        private VisualElement m_Content;
        private Image m_ContentImage;
        private Label m_ContentTitle;
        private VisualElement m_ContentLabelContainer;
        private TextField m_AddLabelField;
        private Button m_AddLabelButton;

        internal Dictionary<string, VisualElement> activeLabels { get; private set; } = new(0);
        //private Action<string[]> onLabelsChange = null;
        internal event Action<string> onLabelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;

        public override VisualElement contentContainer => null;

        IEnumerable<PrefabData> inspectTargets = null;


#if !UNITY_2023_2_OR_NEWER
        public new class UxmlFactory : UxmlFactory<VisualElement, UxmlTraits> {}
        public new class UxmlTraits : VisualElement.UxmlTraits{}
#endif

        public PrefabInspectorView()
        {
            PopulateElement();
            ClearContent();
        }

        private void PopulateElement()
        {
            AddToClassList(ussClassName);

            ///----------------------------------------------
            ///-------------  Header Toolbar  ---------------
            ///----------------------------------------------
            var toolbar = new Toolbar();
            hierarchy.Add(toolbar);

            //Header label
            var toolbar_label = new Label(HEADER_TITLE);
            toolbar.Add(toolbar_label);

            //Toolbar flex space
            var toolbar_space = new ToolbarSpacer();
            toolbar_space.style.flexGrow = 1;
            toolbar.Add(toolbar_space);

            //Toolbar dropdown menu
            var toolbarMenu = new ToolbarMenu();
            toolbarMenu.
                Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage =
                new StyleBackground(UIExtensions.GetEditorIcon("_Menu@2x"));
            m_ToolbarDropdown = toolbarMenu.menu;
            toolbar.Add(toolbarMenu);

            ///----------------------------------------------
            ///-------------  Inspect Content  --------------
            ///----------------------------------------------

            ///Scroll view to wrap the content container
            ///Just in case our height is too little
            ScrollView content_scroll = new ScrollView
            {
                mode = ScrollViewMode.Vertical,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
                style = {
                    flexGrow = 1,
                    overflow = Overflow.Hidden,

                }
            };
            content_scroll.AddToClassList(m_contentUssClassName + "__scroll");
            hierarchy.Add(content_scroll);

            //Main content wrapper
            m_Content = new VisualElement();
            m_Content.AddToClassList(m_contentUssClassName);
            content_scroll.Add(m_Content);

            //Image
            m_ContentImage = new Image(); //Main image element
            m_ContentImage.scaleMode = ScaleMode.ScaleToFit;
            var imageWrapper = new VisualElement(); //Wrapper
            imageWrapper.AddToClassList(m_contentImageUssClassName); //Add classname to wrapper

            imageWrapper.Add(m_ContentImage); //Add image to wrapper
            m_Content.Add(imageWrapper); //Add wrapper to content

            //Details container
            var info = new VisualElement();
            info.AddToClassList("info");
            m_Content.Add(info);

            //Label
            m_ContentTitle = new("%content-title%");
            m_ContentTitle.AddToClassList(m_contentTitleUssClassName);
            info.Add(m_ContentTitle);

            //Add Label field above tags
            m_AddLabelField = new TextField();
            m_AddLabelField.label = "Labels";

#if UNITY_2023_2_OR_NEWER
            m_AddLabelField.textEdition.placeholder = "new label"; //placeholder
            m_AddLabelField.maxLength = TAG_ADD_MAX_LENGTH;
#endif
            m_AddLabelField.AddToClassList(m_LabelFieldUssClassName);
            //Add tag from field when field is submitted
            m_AddLabelField.RegisterCallback<KeyDownEvent>(evt => CatchFieldSubmit(evt, AddLabelFromField), TrickleDown.TrickleDown);
            info.Add(m_AddLabelField);

            //Add Label Button
            m_AddLabelButton = new Button();
            m_AddLabelButton.text = "+";
            m_AddLabelButton.clicked += AddLabelFromField;

            m_AddLabelField.Q(className: "unity-text-field").Add(m_AddLabelButton);

            //Label container
            m_ContentLabelContainer = new VisualElement();
            m_ContentLabelContainer.AddToClassList(m_LabelContainerUssClassName);
            info.Add(m_ContentLabelContainer);
        }
        VisualElement m_DifferingLabelsElement;
        internal void SetContent(IEnumerable<PrefabData> assets)
        {
            //Empty
            if (assets == null || assets.Count() == 0)
            {
                ClearContent();
                return;
            }

            //Single Item
            if (assets.Count() == 1)
            {
                m_Content.SetEnabled(true);
                var asset = assets.First();
                SetContent(AssetPreview.GetAssetPreview(asset.prefab), asset.name, asset.labels);
                inspectTargets = assets;
            }
            //Batch
            else
            {
                var labels = LabelUtilities.GetBatchLabelData(assets);

                m_Content.SetEnabled(true);
                m_ContentImage.image = null;
                m_ContentImage.SetEnabled(false);
                m_ContentTitle.text = $"{assets.Count()} items";

                SetLabels(labels.SharedLabels);

                if(labels.DifferingLabels?.Length > 0)
                {
                    m_DifferingLabelsElement = new AssetLabelElement("Differing Labels", null, Backend_RemoveDifferingLabels, true);
                    m_DifferingLabelsElement.tooltip = string.Join(", ", labels.DifferingLabels);
                    m_ContentLabelContainer.Add(m_DifferingLabelsElement);
                }
                inspectTargets = assets;
            }
        }


        internal void ClearContent()
        {
            SetContent(null, null, null);
            m_Content.SetEnabled(false);
        }
        
        private void SetContent(Texture preview, string title, List<string> tags)
        {
            m_Content.SetEnabled(true);

            m_ContentImage.SetEnabled(preview!=null);
            m_ContentImage.image = preview ?? null;
            m_ContentTitle.text = !string.IsNullOrEmpty(title) ? title : "Nothing To Show";
            SetLabels(tags);
        }
        

        /// <summary>
        /// Set a list of tags for the displayed content.
        /// Provide null to clear tags.
        /// </summary>
        private void SetLabels(IEnumerable<string> tags)
        {
            //Clear tags
            m_ContentLabelContainer.Clear();
            activeLabels.Clear();

            if (tags == null || tags.Count() == 0)
                return;

            foreach (var tag in tags)
            {
                var color = LabelUtilities.GetLabelColor(tag);
                AddLabel(tag, color);
            }

            
        }
        private void AddLabel(string text, Color? color)
        {

            if (string.IsNullOrEmpty(text) || activeLabels.ContainsKey(text))
                return;
            var label = new AssetLabelElement(text,color, RemoveLabel, false);
            label.onClick += onLabelClicked;
            label.onContextMenu += onLabelContextMenu;

            if (m_DifferingLabelsElement != null)
                m_ContentLabelContainer.Insert(activeLabels.Count, label);
            else
                m_ContentLabelContainer.Add(label);


            activeLabels.Add(text, label);

            void RemoveLabel(string text)
            {
                //Remove tag VisualElement
                if (activeLabels.TryGetValue(text, out var ve))
                    m_ContentLabelContainer.Remove(ve);

                activeLabels.Remove(text);
                Backend_RemoveLabel(text); //Backend asset operations
            }
        }

        //private void AddLabel(string text, Color? color, bool canRemove = true)
        //{

        //    if (string.IsNullOrEmpty(text) || activeLabels.ContainsKey(text))
        //        return;

        //    #region template
        //    var finalColor = color.HasValue ? color.Value : TAG_COLOR_DEFAULT;
        //    finalColor.a = Mathf.Min(finalColor.a, TAG_COLOR_MAX_OPACITY);

        //    var tag = new VisualElement();
        //    tag.style.backgroundColor = finalColor;
        //    tag.AddToClassList(m_LabelUssClassName);

        //    var label = new Label(text);
        //    tag.Add(label);

        //    //Remove button + callback
        //    if (canRemove)
        //    {
        //        Button remove_button = new(() => RemoveLabel(text));
        //        remove_button.text = "x";
        //        remove_button.tooltip = "Remove label from asset";
        //        tag.Add(remove_button);
        //    }

        //    m_ContentLabelContainer.Add(tag);
        //    #endregion

        //    #region events
        //    //Click event
        //    tag.RegisterCallback<ClickEvent>(evt => onLabelClicked?.Invoke(text));
        //    //Context menu manipulator
        //    tag.AddManipulator(new ContextualMenuManipulator(e => onLabelContextMenu?.Invoke(text, e)));
        //    #endregion




        //    activeLabels.Add(text, tag);
        //    onLabelsChange?.Invoke(activeLabels.Keys.ToArray());
        //}
        
        private void AddLabelFromField()
        {
            string label = m_AddLabelField.value.Trim();
            AddLabel(label, LabelUtilities.GetLabelColor(label));
            m_AddLabelField.value = "";

            Backend_AddLabel(label);
        }
        private void CatchFieldSubmit(KeyDownEvent evt, Action onSubmit)
        {
            // Check if the pressed key is the Enter key (Return key)
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.character == '\n')
            {
                onSubmit.Invoke();

#if UNITY_2023_2_OR_NEWER
                focusController.IgnoreEvent(evt);
#else
                evt.PreventDefault();
#endif
                evt.StopImmediatePropagation();
            }


        }


        private void Backend_AddLabel(string label)
        {
            //Return if label is not valid
            if (string.IsNullOrEmpty(label) || label?.Trim().Length == 0)
                return;

            //Return if not inspecting assets
            if (inspectTargets?.Count() < 1)
                return;

            LabelUtilities.AddLabelsToAssets(inspectTargets, new string[] { label });
        }
        private void Backend_RemoveDifferingLabels(string obj)
        {
            //Return if not inspecting multiple assets
            if (inspectTargets?.Count() <= 1)
                return;

            m_DifferingLabelsElement?.RemoveFromHierarchy();
            var labels = LabelUtilities.GetBatchLabelData(inspectTargets);
            LabelUtilities.RemoveLabelsFromAssets(inspectTargets, labels.DifferingLabels);
        }
        private void Backend_RemoveLabel(string label)
        {
            //Return if label is not valid
            if (string.IsNullOrEmpty(label) || label?.Trim().Length == 0)
                return;

            //Return if not inspecting assets
            if (inspectTargets?.Count() < 1)
                return;

            LabelUtilities.RemoveLabelsFromAssets(inspectTargets, new string[]{label});
        }
    }
}