using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    public static class UIExtensions
    {
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
    }
}