using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Farbod.Prefabbricato.Backend
{
    [Serializable]
    public class UserLabelData
    {
        public string name;
        public Color color;
        public UserLabelData(string name, Color color)
        {
            this.name = name;
            this.color = color;
        }
    }
    internal enum LabelSelection
    {
        /// <summary>
        /// Labels that are indexed and have Prefabs connected to them
        /// </summary>
        IndexedLabels,
        /// <summary>
        /// Labels which have colors assigned to them by the user
        /// </summary>
        ColorAssignedLabels,
        /// <summary>
        /// Indexed labels and assigned labels, even the ones which don't have associated assets
        /// </summary>
        IndexedAndColorAssigned,
    }
    internal static class LabelUtilities
    {
        /// <summary>
        /// Turn a list of serializable UserLabelData objects into a color dictionary
        /// </summary>
        public static Dictionary<string, Color> SerializedLabelToColorDict(IEnumerable<UserLabelData> input)
        {
            return input
                .GroupBy(item => item.name)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().color
                );
        }
        /// <summary>
        /// Turn a list of label color dictionary into a list of serializable UserLabelData objects
        /// </summary>
        public static IEnumerable<UserLabelData> ColorDictToSerializedLabel(Dictionary<string, Color> input)
        {
            return input.Select((kvp, index) => new UserLabelData(kvp.Key, kvp.Value));
        }

        
        public static Dictionary<string, Color> GetProjectLabels(LabelSelection selection, Color fallback)
        {
            Dictionary<string, Color> assignedColors = PrefabbricatoSettings.instance.GetAssignedLabelColors();
            switch (selection)
            {
                case LabelSelection.ColorAssignedLabels:
                    return PrefabbricatoSettings.instance.GetAssignedLabelColors();
                case LabelSelection.IndexedLabels:
                    List<string> index = AssetIndex.LabelToAssetIndex.Keys.ToList();

                    return index.ToDictionary(
                        label => label,
                        label => assignedColors.TryGetValue(label, out Color color) ? color : fallback //Assign colors with defaultColor fallback
                    );
                case LabelSelection.IndexedAndColorAssigned:
                    // Union of indexed labels AND color-assigned labels
                    // Get all labels from both sources
                    List<string> indexedLabels = AssetIndex.LabelToAssetIndex.Keys.ToList();
                    List<string> colorAssignedLabels = assignedColors.Keys.ToList();

                    // Union (distinct combination) of both lists
                    var allLabels = indexedLabels.Union(colorAssignedLabels).ToList();

                    // Build dictionary with colors (prefer coloredLabels, fallback to defaultColor)
                    return allLabels.ToDictionary(
                        label => label,
                        label => assignedColors.TryGetValue(label, out Color color) ? color : fallback
                    );
                default:
                    return new();
            }
        }

        public static Dictionary<string, Color?> GetProjectLabels(LabelSelection selection = LabelSelection.IndexedLabels)
        {
            Dictionary<string, Color> assignedColors = PrefabbricatoSettings.instance.GetAssignedLabelColors();

            switch (selection)
            {
                case LabelSelection.ColorAssignedLabels:
                    // Convert Color to Color? for consistency
                    return assignedColors.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (Color?)kvp.Value
                    );

                case LabelSelection.IndexedLabels:
                    List<string> index = AssetIndex.LabelToAssetIndex.Keys.ToList();

                    return index.ToDictionary(
                        label => label,
                        label => assignedColors.TryGetValue(label, out Color color) ? color : (Color?)null
                    );

                case LabelSelection.IndexedAndColorAssigned:
                    List<string> indexedLabels = AssetIndex.LabelToAssetIndex.Keys.ToList();
                    List<string> colorAssignedLabels = assignedColors.Keys.ToList();

                    var allLabels = indexedLabels.Union(colorAssignedLabels).ToList();

                    return allLabels.ToDictionary(
                        label => label,
                        label => assignedColors.TryGetValue(label, out Color color) ? color : (Color?)null
                    );

                default:
                    return new Dictionary<string, Color?>();
            }
        }

        /// <summary>
        /// Returns assigned color if available, fallback if not.
        /// </summary>
        public static Color GetLabelColor(string label, Color fallback)
        {
            return PrefabbricatoSettings.instance.GetLabelColor(label)?? fallback;
        }
    }
}
