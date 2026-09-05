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
    public partial class PrefabInspectorView: VisualElement
    {
        private readonly static string HEADER_TITLE = "Inspector";
        private readonly static int TAG_ADD_MAX_LENGTH = 16;
        private readonly static Color TAG_COLOR_DEFAULT = Color.mediumAquamarine;
        private readonly static float TAG_COLOR_MAX_OPACITY = 0.3f;

        internal readonly static string ussClassName = "inspector-view";
        private readonly static string m_contentUssClassName = ussClassName+"_content";
        private readonly static string m_contentImageUssClassName = ussClassName + "_content__image";
        private readonly static string m_contentTitleUssClassName = ussClassName + "_content__title";
        private readonly static string m_TagContainerUssClassName = ussClassName + "_content__tags";
        private readonly static string m_TagFieldUssClassName = ussClassName + "_content__tag-field";
        private readonly static string m_TagUssClassName = "prefab-tag";


        internal DropdownMenu m_ToolbarDropdown;
        private VisualElement m_Content;
        private Image m_ContentImage;
        private Label m_ContentTitle;
        private VisualElement m_ContentTagContainer;
        private TextField m_AddTagField;
        private Button m_AddTagButton;

        internal Dictionary<string, VisualElement> activeTags { get; private set; } = new(0);
        private Action<string[]> onLabelsChange = null;
        internal event Action<string> onLabelClicked;
        internal event Action<string, ContextualMenuPopulateEvent> onLabelContextMenu;

        public override VisualElement contentContainer => null;

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
                Q(className:ToolbarMenu.arrowUssClassName)
                .style.backgroundImage =
                new StyleBackground(UIExtensions.GetEditorIcon("_Menu@2x"));
            m_ToolbarDropdown = toolbarMenu.menu;
            toolbar.Add(toolbarMenu);

            ///----------------------------------------------
            ///-------------  Inspect Content  --------------
            ///----------------------------------------------

            ///Scroll view to wrap the content container
            ///Just in case our height is too little
            var content_scroll = new ScrollView();
            content_scroll.AddToClassList(m_contentUssClassName + "__scroll");
            content_scroll.mode = ScrollViewMode.Vertical;
            content_scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            content_scroll.style.flexGrow = 1;
            content_scroll.style.overflow = Overflow.Hidden;
            hierarchy.Add(content_scroll);

            //Main content wrapper
            m_Content = new VisualElement();
            m_Content.AddToClassList(m_contentUssClassName);
            content_scroll.Add(m_Content);

            //Image
            m_ContentImage = new Image(); //Main image element
            m_ContentImage.scaleMode = ScaleMode.ScaleAndCrop;
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

            //Add Tag field above tags
            m_AddTagField = new TextField();
            m_AddTagField.label = "Labels";

#if UNITY_2023_2_OR_NEWER
            m_AddTagField.textEdition.placeholder = "new label"; //placeholder
            m_AddTagField.maxLength = TAG_ADD_MAX_LENGTH;
#endif
            m_AddTagField.AddToClassList(m_TagFieldUssClassName);
            //Add tag from field when field is submitted
            m_AddTagField.RegisterCallback<KeyDownEvent>(evt=>CatchFieldSubmit(evt, AddTagFromField), TrickleDown.TrickleDown);

            info.Add(m_AddTagField);

            //Add Tag Button
            m_AddTagButton = new Button();
            m_AddTagButton.text = "+";
            m_AddTagButton.clicked += AddTagFromField;

            m_AddTagField.Q(className:"unity-text-field").Add(m_AddTagButton);

            //Tag container
            m_ContentTagContainer = new VisualElement();
            m_ContentTagContainer.AddToClassList(m_TagContainerUssClassName);
            info.Add(m_ContentTagContainer);
        }
        internal void ClearContent()
        {
            SetContent(null, null, null);
            //m_Content.SetEnabled(false);
        }
        internal void SetContent(Texture preview, string title, string[] tags, Action<string[]> onTagsChange = null)
        {
            m_Content.SetEnabled(true);

            m_ContentImage.image = preview ?? null;
            m_ContentTitle.text = !string.IsNullOrEmpty(title)?title: "Nothing To Show";
            this.onLabelsChange=onTagsChange;
        }

        /// <summary>
        /// Set a list of tags for the displayed content.
        /// Provide null to clear tags.
        /// </summary>
        private void SetTags(Dictionary<string, Color> tags)
        {
            //Clear tags
            m_ContentTagContainer.Clear();
            activeTags.Clear();

            if (tags == null || tags.Count == 0)
                return;

            foreach (var item in tags)
            {
                AddTag(item.Key, item.Value);
            }
        }

        private void AddTag(string text, Color? color, bool canRemove = true) {

            if (string.IsNullOrEmpty(text) || activeTags.ContainsKey(text))
                return;

            #region template
            var finalColor = color.HasValue ? color.Value : TAG_COLOR_DEFAULT;
            finalColor.a = Mathf.Min(finalColor.a, TAG_COLOR_MAX_OPACITY);

            var tag = new VisualElement();
            tag.style.backgroundColor = finalColor;
            tag.AddToClassList(m_TagUssClassName);

            var label = new Label(text);
            tag.Add(label);

            //Remove button + callback
            if(canRemove)
            {
                Button remove_button = new(()=>RemoveTag(text));
                remove_button.text = "x";
                remove_button.tooltip = "Remove label from asset";
                tag.Add(remove_button);
            }

            m_ContentTagContainer.Add(tag);
            #endregion

            #region events
            //Click event
            tag.RegisterCallback<ClickEvent>(evt => onLabelClicked?.Invoke(text));
            //Context menu manipulator
            tag.AddManipulator(new ContextualMenuManipulator(e => onLabelContextMenu?.Invoke(text, e)));
            #endregion


            

            activeTags.Add(text, tag);
            onLabelsChange?.Invoke(activeTags.Keys.ToArray());
        }
        private void RemoveTag(string text)
        {
            //Remove tag VisualElement
            if (activeTags.TryGetValue(text, out var ve))
                m_ContentTagContainer.Remove(ve);

            activeTags.Remove(text);

            if (onLabelsChange != null)
            {
                onLabelsChange.Invoke(activeTags.Keys.ToArray());
            }
        }
        private void AddTagFromField()
        {
            AddTag(m_AddTagField.value.Trim(), TAG_COLOR_DEFAULT);
            m_AddTagField.value = "";
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
    }
}