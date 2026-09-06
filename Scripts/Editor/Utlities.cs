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
        public static Color? GetLabelColor(string label)
        {
            return PrefabbricatoSettings.instance.GetLabelColor(label);
        }
    }
    internal static class PathUtilities
    {
        public static string GetAbsolutePathFromProject(string relativePath)
        {
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", relativePath));
        }

        public static void OpenInFileBrowser(string path)
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                OpenInLinuxFileBrowser(path);
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                OpenInMacFileBrowser(path);
            }
            else // assume Windows
            {
                OpenInWinFileBrowser(path);
            }
        }

        public static void OpenInLinuxFileBrowser(string path)
        {
            bool openInsidesOfFolder = false;

            string linuxPath = path.Replace("\\", "/"); // linux  doesn't like backward slashes

            if (System.IO.Directory.Exists(linuxPath)) // if path requested is a folder, automatically open insides of that folder
            {
                openInsidesOfFolder = true;
            }

            try
            {
                // https://askubuntu.com/a/1424380
                // Note: xdg-open only works properly when given a folder.
                // If given a path to a file, xdg-open will open that file with the associated program.
                // So we use dbus-send instead if we're showing a file.

                string processName;
                string arguments;
                if (openInsidesOfFolder)
                {
                    processName = "xdg-open";
                    arguments = $"\"{linuxPath}\"";
                }
                else
                {
                    processName = "dbus-send";
                    arguments = $"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://{linuxPath}\" string:\"\"";
                }

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    CreateNoWindow = false,
                    UseShellExecute = false,
                    FileName = processName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                System.Diagnostics.Process.Start(processStartInfo);
            }
            catch (System.Exception e)
            {
                e.HelpLink = ""; // do anything with this variable to silence warning about not using it
                                 //Debug.LogError($"{e}");

#if UNITY_EDITOR
                // EditorUtility.RevealInFinder is sure to work, but for files, it doesn't allow us to pre-select the file specified.
                // For folders, it can't open the insides of a folder, instead it will open the parent folder.
                // Very strange behavior, so we use EditorUtility.RevealInFinder only as our last resort.
                UnityEditor.EditorUtility.RevealInFinder(path);
#endif
            }
        }

        public static void OpenInMacFileBrowser(string path)
        {
            bool openInsidesOfFolder = false;

            // try mac
            string macPath = path.Replace("\\", "/"); // mac finder doesn't like backward slashes

            if (System.IO.Directory.Exists(macPath)) // if path requested is a folder, automatically open insides of that folder
            {
                openInsidesOfFolder = true;
            }

            if (!macPath.StartsWith("\""))
            {
                macPath = "\"" + macPath;
            }

            if (!macPath.EndsWith("\""))
            {
                macPath = macPath + "\"";
            }

            string arguments = (openInsidesOfFolder ? "" : "-R ") + macPath;
            try
            {
                System.Diagnostics.Process.Start("open", arguments);
            }
            catch (System.Exception e)
            {
                e.HelpLink = ""; // do anything with this variable to silence warning about not using it

#if UNITY_EDITOR
                // EditorUtility.RevealInFinder is sure to work, but for files, it doesn't allow us to pre-select the file specified.
                // For folders, it can't open the insides of a folder, instead it will open the parent folder.
                // Very strange behavior, so we use EditorUtility.RevealInFinder only as our last resort.
                UnityEditor.EditorUtility.RevealInFinder(path);
#endif
            }
        }

        public static void OpenInWinFileBrowser(string path)
        {
            bool openInsidesOfFolder = false;

            // try windows
            string winPath = path.Replace("/", "\\"); // windows explorer doesn't like forward slashes

            if (System.IO.Directory.Exists(winPath)) // if path requested is a folder, automatically open insides of that folder
            {
                openInsidesOfFolder = true;
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", (openInsidesOfFolder ? "/root," : "/select,") + winPath);
            }
            catch (System.Exception e)
            {
                e.HelpLink = ""; // do anything with this variable to silence warning about not using it

#if UNITY_EDITOR
                // EditorUtility.RevealInFinder is sure to work, but for files, it doesn't allow us to pre-select the file specified.
                // For folders, it can't open the insides of a folder, instead it will open the parent folder.
                // Very strange behavior, so we use EditorUtility.RevealInFinder only as our last resort.
                UnityEditor.EditorUtility.RevealInFinder(path);
#endif
            }
        }
    }

    public static class TimeSpanExtensions
    {
        public static string ToShortString(this TimeSpan self)
        {
            if (self == null)
                return null;
            if (self.TotalSeconds < 60)
                return $"{self.Seconds} seconds ago";
            else if (self.TotalMinutes < 60)
                return $"{self.Minutes} minutes ago";
            else if (self.TotalHours < 24)
                return $"{self.Hours} hours ago";
            else if (self.TotalDays < 30)
                return $"{self.Days} days ago";
            else if (self.TotalDays < 365)
                return $"{Mathf.FloorToInt(self.Days / 30)} months ago";

            int years = Mathf.FloorToInt(self.Days / 365);
            if (years < 8)
                return $"{years} years ago";
            else
                return $"an absurdly long time ago";
        }
    }
}
