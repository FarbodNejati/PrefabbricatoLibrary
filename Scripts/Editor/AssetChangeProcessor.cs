using System;
using UnityEditor;

namespace Farbod.Prefabbricato.Backend
{
    /// <summary>
    /// Watches the AssetDatabase for anything that could invalidate <see cref="AssetIndex"/> -
    /// prefab creation, deletion, moves/renames, and label edits (which Unity surfaces as a
    /// reimport of the asset's .meta file) - and keeps the index in sync incrementally, without
    /// requiring a full re-scan.
    /// </summary>
    /// <remarks>
    /// This class is intentionally "dumb": it only translates raw AssetDatabase events into calls
    /// on AssetIndex's public Notify* API. It knows nothing about labels, UI, or how the index is
    /// stored, so it stays decoupled from both <see cref="LabelUtilities"/> (which never needs to
    /// call into this directly - editing labels through AssetDatabase.SetLabels naturally triggers
    /// this class as a reimport) and from any UI. Anyone who mutates asset labels or the library
    /// contents in a different way will still get picked up here, as long as it goes through the
    /// AssetDatabase.
    /// </remarks>
    internal class PrefabbricatoAssetProcessor : AssetPostprocessor
    {
        private const string PREFAB_EXTENSION = ".prefab";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            //Nothing to update incrementally until the index has been built at least once.
            if (!AssetIndex.IsIndexed)
                return;

            bool anyChange = false;

            //--------- Moved / renamed assets (GUID is preserved by Unity across a move) ---------
            for (int i = 0; i < movedAssets.Length; i++)
            {
                string newPath = movedAssets[i];
                string oldPath = movedFromAssetPaths[i];

                if (!IsPrefabPath(newPath))
                    continue;

                bool wasInLibrary = AssetIndex.IsUnderScanPath(oldPath);
                bool isInLibrary = AssetIndex.IsUnderScanPath(newPath);

                if (wasInLibrary && isInLibrary)
                {
                    string guid = AssetDatabase.AssetPathToGUID(newPath);
                    anyChange |= AssetIndex.NotifyAssetMoved(guid, newPath);
                }
                else if (!wasInLibrary && isInLibrary)
                {
                    //Moved into the library from elsewhere - treat as a new addition
                    string guid = AssetDatabase.AssetPathToGUID(newPath);
                    anyChange |= AssetIndex.NotifyAssetAddedOrUpdated(guid);
                }
                else if (wasInLibrary && !isInLibrary)
                {
                    //Moved out of the library - treat as a removal
                    string guid = AssetIndex.FindGuidByPath(oldPath);
                    anyChange |= AssetIndex.NotifyAssetRemoved(guid);
                }
            }

            //--------- Imported assets: newly created prefabs, content reimports, and label edits ---------
            //(AssetDatabase.SetLabels() rewrites the asset's .meta file, which Unity reports here
            //as an import of that asset - so both "created" and "labels changed" land in this list.
            //AssetIndex diffs against what it already knows to tell the two apart.)
            foreach (string path in importedAssets)
            {
                if (!IsPrefabPath(path) || !AssetIndex.IsUnderScanPath(path))
                    continue;

                string guid = AssetDatabase.AssetPathToGUID(path);
                anyChange |= AssetIndex.NotifyAssetAddedOrUpdated(guid);
            }

            //--------- Deleted assets ---------
            foreach (string path in deletedAssets)
            {
                if (!IsPrefabPath(path) || !AssetIndex.IsUnderScanPath(path))
                    continue;

                //The asset is already gone, so it can't be resolved through the AssetDatabase
                //anymore - look up its GUID from what the index already has on record.
                string guid = AssetIndex.FindGuidByPath(path);
                anyChange |= AssetIndex.NotifyAssetRemoved(guid);
            }

            if (anyChange)
                AssetIndex.NotifyIndexUpdated();
        }

        private static bool IsPrefabPath(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) &&
            assetPath.EndsWith(PREFAB_EXTENSION, StringComparison.OrdinalIgnoreCase);
    }
}
