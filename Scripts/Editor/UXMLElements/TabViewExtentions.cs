using UnityEngine.UIElements;

namespace Farbod.Prefabbricato
{
    public static class TabViewExtensions
    {
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
    }
}