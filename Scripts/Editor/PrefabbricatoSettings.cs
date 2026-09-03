using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;

namespace Farbod.Prefabbricato
{
    internal static class PrefabbricatoSettings
    {
        internal static readonly string PROJECT_ASSET_PATH = "Assets";

        /// <summary>
        /// The selected path to perform asset library scans, relative to the project root.
        /// </summary>
        internal static string LibraryPath { get; private set; } = string.Empty;

        internal static event Action OnRootChange;

        [InitializeOnLoadMethod]
        static void LoadSavedData()
        {
            LibraryPath = EditorDataManager.GetLibraryPath(); //Get library path from saved data (if available)
        }

        /// <summary>
        /// Is the library directory set up and selected.
        /// </summary>
        internal static bool IsSetUp()
        {
            /// Check if root is assigned
            if (string.IsNullOrEmpty(LibraryPath))
                return false;

            /// Check if root is valid, and clear root if not!
            /// (clearing it avoids performing a validity check later on)
            if (!IsProjectPathValidForLibrary(LibraryPath))
            {
                LibraryPath = "";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Open an explorer popup to select the folder for library scanning.
        /// </summary>
        /// <returns>Success Result</returns>
        internal static bool SelectLibraryDirectory()
        {
            //Start at selected folder (if inside project)
            string startingPath = AssetDatabase.IsValidFolder(LibraryPath) ? LibraryPath : PROJECT_ASSET_PATH;

            //Open folder selection window
            string folderPath = EditorUtility.OpenFolderPanel("Select Library Root", startingPath, "");

            //Cancelled by user (no path selected)
            if(string.IsNullOrEmpty(folderPath))
                return false;


            //Validation check
            while (!IsProjectPathValidForLibrary(FileUtil.GetProjectRelativePath(folderPath))) //Re open selection panel
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

            //Finally, set path.
            LibraryPath = FileUtil.GetProjectRelativePath(folderPath);
            EditorDataManager.SaveLibraryPath(LibraryPath);
            OnRootChange?.Invoke();
            return true;
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
