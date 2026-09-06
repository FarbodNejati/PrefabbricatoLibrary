using Farbod.Prefabbricato;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.Audio.ProcessorInstance.AvailableData;

internal class AssetLabelElement : VisualElement
{
    private readonly static Color LABEL_COLOR_DEFAULT = Color.lightBlue;
    private readonly static float TAG_COLOR_DEFAULT_INTENSITY = 0.25f;


    internal readonly static string ussClassName = "asset-label";
    internal readonly static string nameUssClassName = ussClassName + "__name";
    internal readonly static string removeButtonUssClassName = ussClassName + "__remove";
    private static readonly Background s_IconImage = UIExtensions.GetEditorIcon("FilterByLabel");


    private float m_ColorIntensity = TAG_COLOR_DEFAULT_INTENSITY;
    internal float colorIntensity
    {
        get => m_ColorIntensity;
        set {
            m_ColorIntensity = value;
            SetColor(style.backgroundColor.value);
        }
    }

    internal event Action<string> onClick;
    internal event Action<string, ContextualMenuPopulateEvent> onContextMenu;

    /// <summary>
    /// Create a visual element to represent AssetLabels
    /// </summary>

    internal AssetLabelElement()
    {
        //var chip = new VisualElement();
        //hierarchy.Add(chip);

        AddToClassList(ussClassName);

        var icon = new VisualElement()
        {
            style =
            {
                width = 12,
                height = 12,
                backgroundImage = s_IconImage
            }
        };
        hierarchy.Add(icon);

        var nameLabel = new Label("Label");
        nameLabel.AddToClassList(nameUssClassName);
        nameLabel.style.flexGrow = 1;
        hierarchy.Add(nameLabel);

    }

    internal AssetLabelElement(
        string labelName,
        Color? color,
        Action<string> onRemove = null,
        bool hasIcon = true)
    {
        AddToClassList(ussClassName);

        if (hasIcon)
        {
            var icon = new VisualElement();
            icon.style.minWidth = 12;
            icon.style.minHeight = 12;
            icon.style.backgroundImage = s_IconImage;
            hierarchy.Add(icon);
        }
        

        var nameLabel = new Label(labelName);
        nameLabel.AddToClassList(nameUssClassName);
        nameLabel.style.flexGrow = 1;
        hierarchy.Add(nameLabel);

        RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation(); // don't let this bubble into row/item selection
            onClick?.Invoke(labelName);
        });

        this.AddManipulator(new ContextualMenuManipulator(e => onContextMenu?.Invoke(labelName, e)));

        if (onRemove != null)
        {
            Button remove_button = new(() => onRemove.Invoke(name));
            remove_button.AddToClassList(removeButtonUssClassName);
            remove_button.text = "x";
            remove_button.tooltip = "Remove label from asset";
            hierarchy.Add(remove_button);
        }
        SetColor(color);
    }

    public void SetColor(Color? color)
    {
        var modifiedColor = color.HasValue ? color.Value : LABEL_COLOR_DEFAULT;
        //Alpha of given color is overridden
        modifiedColor.a = colorIntensity;
        style.backgroundColor = modifiedColor;
    }
}
