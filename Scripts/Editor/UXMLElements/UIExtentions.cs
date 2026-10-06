using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    internal static class UIExtensions
    {
        public static void RegisterFieldSubmitCallback(this TextField self, Action callback)
        {
            self.RegisterCallback<KeyDownEvent>(evt => CatchFieldSubmit(self, evt, callback), TrickleDown.TrickleDown);
        }
        public static void RegisterFieldSubmitCallback(this ToolbarSearchField self, Action callback)
        {
            self.RegisterCallback<KeyDownEvent>(evt => CatchFieldSubmit(self, evt, callback), TrickleDown.TrickleDown);
        }
        public static void RegisterFieldSubmitCallback(this ToolbarPopupSearchField self, Action callback)
        {
            self.RegisterCallback<KeyDownEvent>(evt => CatchFieldSubmit(self, evt, callback), TrickleDown.TrickleDown);
        }
        private static void CatchFieldSubmit(VisualElement ve,KeyDownEvent evt, Action callback)
        {
            // Check if the pressed key is the Enter key (Return key)
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.character == '\n')
            {
                callback.Invoke();

#if UNITY_2023_2_OR_NEWER
                ve.focusController.IgnoreEvent(evt);
#else
                evt.PreventDefault();
#endif
                evt.StopImmediatePropagation();
            }


        }

        public static UnityEngine.UIElements.Background GetEditorIcon(string name)
        {
            Texture2D icon = (Texture2D)EditorGUIUtility.IconContent(name).image;
            return Background.FromTexture2D(icon);
        }
        /// <summary>
        /// This adds the appropriate class names to a tab view's headers
        /// to make them look like a button group.
        /// </summary>
        /// <param name="self"></param>
        public static void MakeHeaderStyleButtonGroup(this TabView self)
        {
            //Header Container Wrapper
            var contentContainer = self.Q<VisualElement>(className: TabView.viewportUssClassName);
            //Remove default bg color
            contentContainer.style.backgroundColor = new StyleColor(new UnityEngine.Color(0, 0, 0, 0));

            //Header Container
            var headerContainer = self.Q<VisualElement>(className: TabView.headerContainerClassName);
            headerContainer.AddToClassList(ToggleButtonGroup.ussClassName);
            

            //Header buttons
            var headerButtons = headerContainer.Query<VisualElement>(className: Tab.tabHeaderUssClassName);
            headerButtons.First().AddToClassList(ToggleButtonGroup.buttonLeftClassName);
            headerButtons.Last().AddToClassList(ToggleButtonGroup.buttonRightClassName);
            headerButtons.ForEach(e =>
            {
                e.RemoveFromClassList(Tab.tabHeaderUssClassName); //Remove default style
                e.AddToClassList(Button.ussClassName);
                e.AddToClassList(ToggleButtonGroup.buttonClassName);
            });
        }

        public static ToolbarMenu WithIcon(this ToolbarMenu self, string iconContent)
        {
            var img = GetEditorIcon(iconContent);
            if(img == null)
                return self;

            self.
                Q(className: ToolbarMenu.arrowUssClassName)
                .style.backgroundImage = img;

            return self;
        }

        public static DropdownMenu AppendAction(this DropdownMenu self, string actionName, Action<DropdownMenuAction> action, bool enabled = true)
        {
            DropdownMenuAction.Status status = enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            self.AppendAction(actionName, action, status);

            return self;
        }
    }
}