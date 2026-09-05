using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Farbod.Prefabbricato.Backend
{
    /// <summary>
    /// Project wide settings for the Prefabbricato library
    /// </summary>
    [FilePath("Prefabbricato/Settings.json", FilePathAttribute.Location.ProjectFolder)]
    public class PrefabbricatoSettings : ScriptableSingleton<PrefabbricatoSettings>
    {
        private const bool SAVE_AS_TEXT = true;
        private const string PROJECT_ASSET_PATH = "Assets";

        /// <summary>
        /// Invoked when LibraryPath changes
        /// </summary>
        internal static event Action<string> onLibraryChange;
        internal static event Action<string> onLabelColorUpdate;


        /// <summary>
        /// The path in which we scan for prefab assets
        /// </summary>
        [SerializeField]
        private string libraryPath = string.Empty;

        public static string LibraryPath => instance.libraryPath;

        /// <summary>
        /// User assigned data for labels
        /// </summary>
        [SerializeField]
        private List<UserLabelData> userLabelData = new();

        /// <summary>
        /// Is the library directory set up, and valid?
        /// </summary>
        internal static bool IsLibrarySetUp()
        {
            // Check if root is assigned
            if (string.IsNullOrEmpty(instance.libraryPath))
                return false;

            // Check if root is valid, and clear root if not!
            if (!IsProjectPathValidForLibrary(instance.libraryPath))
            {
                instance.libraryPath = "";// Clear assigned path to avoid validity check later
                return false;
            }
            return true;
        }


        /// <summary>
        /// Open an explorer popup to select the folder for library scanning.
        /// </summary>
        /// <returns>Success Result</returns>
        public static bool SelectLibraryDirectory()
        {
            string oldPath = instance.libraryPath;
            //Start at selected folder (if inside project)
            string startingPath = AssetDatabase.IsValidFolder(instance.libraryPath) ? instance.libraryPath : PROJECT_ASSET_PATH;

            //Open folder selection window
            string folderPath = EditorUtility.OpenFolderPanel("Select Library Root", startingPath, "");

            //Cancelled by user (no path selected)
            if (string.IsNullOrEmpty(folderPath))
                return false;

            var relativePath = FileUtil.GetProjectRelativePath(folderPath);

            //Validation check
            while (!IsProjectPathValidForLibrary(relativePath)) //Re open selection panel
            {
                //Cancelled by user (from folder panel)
                if (string.IsNullOrEmpty(folderPath))
                    return false;

                //Popup to reselect
                bool shouldRetry = EditorUtility.DisplayDialog("Invalid Folder", "Please select a sub-folder of your project's Assets directory for scanning.", "OK", "Cancel");
                //Cancelled by user (from error dialog)
                if (!shouldRetry)
                    return false;

                folderPath = EditorUtility.OpenFolderPanel("Select Library Root", folderPath, "");
            }

            //Finally, update path and save.
            instance.libraryPath = relativePath;
            instance.Save(SAVE_AS_TEXT);

            //Notify other scripts.
            if (instance.libraryPath != oldPath)
                onLibraryChange?.Invoke(relativePath);
            return true;
        }

        /// <summary>
        /// Colors assigned to labels by the user.
        /// </summary>
        public Dictionary<string, Color> GetAssignedLabelColors() => LabelUtilities.SerializedLabelToColorDict(userLabelData);
        public Color? GetLabelColor(string label)
        {
            var assigned = userLabelData.Find(l => l.name == label);
            return assigned?.color ?? null;
        }
        /// <summary>
        /// Assign colors to labels by name (null color value will remove existing assignment)
        /// </summary>
        public void AssignColorToLabels(Dictionary<string, Color?> assignment)
        {
            var labelDict = userLabelData.ToDictionary(l => l.name, l => l);

            foreach (var kvp in assignment)
            {
                if (labelDict.TryGetValue(kvp.Key, out var existing))
                {
                    if (kvp.Value.HasValue)
                        existing.color = kvp.Value.Value; //Replace old assigned color
                    else
                        userLabelData.Remove(existing); //Remove assignment if value is null
                }
                //Add new color assignment
                else if (kvp.Value.HasValue)
                {
                    userLabelData.Add(new UserLabelData(kvp.Key, kvp.Value.Value));
                }

                //Alert color update
                onLabelColorUpdate?.Invoke(kvp.Key);
            }

            //Finally save out file
            Save(SAVE_AS_TEXT);
        }

        /// <summary>
        /// Check validity of path as library path.
        /// </summary>
        /// <param name="relativePath">Project Relative Path</param>
        private static bool IsProjectPathValidForLibrary(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return false;

            //Validator
            bool condition =
                relativePath.StartsWith(PROJECT_ASSET_PATH) && //Relative path starts in Assets folder
                !relativePath.TrimEnd('/').EndsWith(PROJECT_ASSET_PATH) &&  //Is not the Assets folder itself
                AssetDatabase.IsValidFolder(relativePath); //Is a valid folder for the unity asset database

            return condition;
        }

    }
    
}
