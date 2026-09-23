using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Farbod.Prefabbricato.Backend
{
    internal static class AssetIndex
    {
        /// <summary>
        /// The path used for finding and indexing assets.
        /// </summary>
        private static string SCAN_PATH => PrefabbricatoSettings.LibraryPath;

        /// <summary>
        /// At how many days is the asset index considered stale?
        /// </summary>
        private static readonly int INDEX_STALE_THRESHOLD_DAYS = 7;
        internal static bool IsStale => IsIndexed ? LastIndexSpan.TotalDays > INDEX_STALE_THRESHOLD_DAYS : false;

        /// <summary>
        /// Has our index been built at least once?
        /// </summary>
        internal static bool IsIndexed { get; private set; }

        internal static DateTime m_LastIndexTime;
        internal static DateTime LastIndexTime
        {
            get => m_LastIndexTime;
            set
            {
                m_LastIndexTime = value;
            }
        }

        internal static string m_LastIndexPath;
        /// <summary>
        /// How long ago the last indexing scan operation took place
        /// </summary>
        internal static TimeSpan LastIndexSpan => DateTime.Now - m_LastIndexTime;

        /// <summary>
        /// Each string Label name points to a hashset that has all indexed prefab GUIDs
        /// </summary>
        internal static Dictionary<string, HashSet<string>> LabelToAssetIndex { get; private set; } = new();

        /// <summary>
        /// Each indexed Prefab GUID to its labels.
        /// </summary>
        internal static Dictionary<string, HashSet<string>> AssetToLabelIndex { get; private set; } = new();

        internal static List<PrefabData> PrefabDataList { get; private set; } = new();
        /// <summary>
        /// Each indexed Prefab GUID to its data.
        /// </summary>
        //internal static Dictionary<string, HashSet<string>> AssetGUIDToAssetDataIndex { get; private set; } = new();

        /// <summary>
        /// Fired any time the index changes for any reason (full rebuild, or any of the granular
        /// events below). This is the catch-all UI should subscribe to for a "just repaint everything"
        /// response. Fired at most once per batch of changes coming from the asset processor.
        /// </summary>
        internal static event Action onIndexUpdate;

        /// <summary>
        /// Fired when a new asset has been added to the index (created, imported, or moved into
        /// the library path). Carries the newly created <see cref="PrefabData"/>.
        /// </summary>
        internal static event Action<PrefabData> onAssetAdded;

        /// <summary>
        /// Fired when an indexed asset has been removed (deleted, or moved out of the library path).
        /// Carries the <see cref="PrefabData"/> as it was right before removal, so UI can still show
        /// its name/path/labels (e.g. for a toast notification) after it's gone from the index.
        /// </summary>
        internal static event Action<PrefabData> onAssetRemoved;

        /// <summary>
        /// Fired when an already-indexed asset's labels changed on disk (e.g. the user edited labels
        /// via the inspector, or another tool touched the .meta file).
        /// </summary>
        internal static event Action<PrefabData> onAssetLabelsChanged;

        /// <summary>
        /// Fired when an already-indexed asset was moved/renamed (its GUID is unchanged, only its path).
        /// </summary>
        internal static event Action<PrefabData> onAssetMoved;

        [InitializeOnLoadMethod]
        static void LoadIndexData()
        {
            //Subscribe to app quit for saving idex data
            EditorApplication.quitting += () => SaveIndexData();

            PrefabbricatoSettings.onLibraryChange += (newPath) =>
            {
                if (m_LastIndexPath != newPath)
                    IsIndexed = false;
            };

            //Return if already indexed
            if (IsIndexed)
                return;

            //Load data from disk
            var savedData = IndexSavedDataManager.LoadIndexData();



            //Check if we should load this data
            //(index data not null, and path matches)
            if (savedData?.labelToAssetIndex != null && savedData.indexPath == SCAN_PATH)
            {
                //Load label to asset guid index
                LabelToAssetIndex = new();
                foreach (var kvp in savedData.labelToAssetIndex)
                {
                    // Convert List<string> to HashSet<string>
                    LabelToAssetIndex[kvp.Key] = new HashSet<string>(kvp.Value);
                }

                //Build asset guid to label index
                AssetToLabelIndex = ReverseIndex(LabelToAssetIndex);
                //Build data list
                BuildPrefabDataList();

                IsIndexed = true;
                LastIndexTime = savedData.LastIndexBuildTime;
                m_LastIndexPath = savedData.indexPath;
            }
        }

        /// <summary>
        /// Scan and rebuild index
        /// </summary>
        /// <param name="path"></param>
        internal static void BuildIndex(string path = null)
        {
            if (path == null)
                path = SCAN_PATH;

            //Check Directory validity
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogWarning("[Prefabbricato] Scan Failed.\n" +
                    $"Cannot scan '{path}'. Path does not exist or is not a valid Project folder.");
                return;
            }

            //Get all prefab assets under our root directory
            var prefab_guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });


            //Build index : Asset -> label 
            AssetToLabelIndex.Clear();
            foreach (var guid in prefab_guids)
            {
                AssetToLabelIndex[guid] = AssetDatabase.GetLabels(new GUID(guid)).ToHashSet();
            }
            //Build index : Label -> asset
            LabelToAssetIndex = ReverseIndex(AssetToLabelIndex);


            BuildPrefabDataList();

            //Results
            IsIndexed = true;
            LastIndexTime = DateTime.Now;
            m_LastIndexPath = path;
            onIndexUpdate?.Invoke();
            SaveIndexData();
            //Debug
            Debug.Log($"[Prefabbricato] Scan completed. Indexed {prefab_guids.Count()} assets and {LabelToAssetIndex.Count()} labels.");
        }

        private static void BuildPrefabDataList()
        {
            PrefabDataList.Clear();
            foreach (var guid in AssetToLabelIndex.Keys)
            {
                PrefabData data = new(guid, AssetToLabelIndex[guid].ToList());
                if (data.prefab == null)
                    continue; //Stale/broken GUID, skip rather than crash

                PrefabDataList.Add(data);
            }
        }

        #region Incremental Update API

        /// <summary>
        /// Is the given asset path located under the currently scanned library path?
        /// </summary>
        internal static bool IsUnderScanPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(SCAN_PATH))
                return false;

            string root = SCAN_PATH.TrimEnd('/');
            return assetPath == root || assetPath.StartsWith(root + "/", StringComparison.Ordinal);
        }

        internal static PrefabData FindByGuid(string guid) =>
            PrefabDataList.FirstOrDefault(d => d.guid == guid);

        internal static string FindGuidByPath(string assetPath) =>
            PrefabDataList.FirstOrDefault(d => d.assetPath == assetPath)?.guid;

        /// <summary>
        /// Adds a brand-new asset to the index, or, if it's already indexed, refreshes its labels
        /// if they changed. Safe to call speculatively - does nothing (and returns false) if the
        /// index hasn't been built yet, or nothing actually changed.
        /// Fires <see cref="onAssetAdded"/> or <see cref="onAssetLabelsChanged"/> as appropriate.
        /// </summary>
        internal static bool NotifyAssetAddedOrUpdated(string guid)
        {
            if (!IsIndexed || string.IsNullOrEmpty(guid))
                return false;

            HashSet<string> newLabels = AssetDatabase.GetLabels(new GUID(guid)).ToHashSet();

            if (!AssetToLabelIndex.TryGetValue(guid, out HashSet<string> oldLabels))
            {
                //Brand new asset
                PrefabData data = new(guid, newLabels.ToList());
                if (data.prefab == null)
                    return false; //Failed to resolve, ignore

                AssetToLabelIndex[guid] = newLabels;
                foreach (string label in newLabels)
                    AddToLabelIndex(label, guid);

                PrefabDataList.Add(data);

                onAssetAdded?.Invoke(data);
                return true;
            }

            if (oldLabels.SetEquals(newLabels))
                return false; //Nothing meaningful changed

            //Update label indices: remove from labels no longer present, add to new ones
            foreach (string label in oldLabels)
            {
                if (!newLabels.Contains(label))
                    RemoveFromLabelIndex(label, guid);
            }
            foreach (string label in newLabels)
            {
                if (!oldLabels.Contains(label))
                    AddToLabelIndex(label, guid);
            }

            AssetToLabelIndex[guid] = newLabels;

            PrefabData existing = FindByGuid(guid);
            if (existing != null)
            {
                existing.labels = newLabels.ToList();
                onAssetLabelsChanged?.Invoke(existing);
            }
            return true;
        }

        /// <summary>
        /// Removes an asset from the index (e.g. it was deleted, or moved outside of the library path).
        /// Fires <see cref="onAssetRemoved"/>. Safe to call speculatively.
        /// </summary>
        internal static bool NotifyAssetRemoved(string guid)
        {
            if (!IsIndexed || string.IsNullOrEmpty(guid))
                return false;

            if (!AssetToLabelIndex.TryGetValue(guid, out HashSet<string> labels))
                return false; //Wasn't indexed to begin with

            foreach (string label in labels)
                RemoveFromLabelIndex(label, guid);

            AssetToLabelIndex.Remove(guid);

            PrefabData removed = FindByGuid(guid);
            if (removed != null)
                PrefabDataList.Remove(removed);

            onAssetRemoved?.Invoke(removed);
            return true;
        }

        /// <summary>
        /// Updates the cached path (and name) of an already-indexed asset after a move/rename.
        /// Fires <see cref="onAssetMoved"/>. Safe to call speculatively.
        /// </summary>
        internal static bool NotifyAssetMoved(string guid, string newPath)
        {
            if (!IsIndexed || string.IsNullOrEmpty(guid))
                return false;

            PrefabData data = FindByGuid(guid);
            if (data == null)
                return false; //Not indexed, nothing to update

            data.UpdatePath(newPath);
            onAssetMoved?.Invoke(data);
            return true;
        }

        /// <summary>
        /// Call once after a batch of Notify* calls to let subscribers know the index has settled.
        /// Also persists the updated index to disk.
        /// </summary>
        internal static void NotifyIndexUpdated()
        {
            if (!IsIndexed)
                return;

            SaveIndexData();
            onIndexUpdate?.Invoke();
        }

        private static void AddToLabelIndex(string label, string guid)
        {
            if (!LabelToAssetIndex.TryGetValue(label, out HashSet<string> set))
            {
                set = new HashSet<string>();
                LabelToAssetIndex[label] = set;
            }
            set.Add(guid);
        }

        private static void RemoveFromLabelIndex(string label, string guid)
        {
            if (!LabelToAssetIndex.TryGetValue(label, out HashSet<string> set))
                return;

            set.Remove(guid);
            if (set.Count == 0)
                LabelToAssetIndex.Remove(label);
        }

        #endregion


        private static void SaveIndexData()
        {
            if (!IsIndexed)
                return;

            Dictionary<string, List<string>> labelAssetIndex = new();
            foreach (string label in LabelToAssetIndex.Keys.ToArray())
            {
                labelAssetIndex[label] = LabelToAssetIndex[label].ToList();
            }

            IndexSavedDataManager.IndexData data = new(labelAssetIndex, LastIndexTime, m_LastIndexPath);
            IndexSavedDataManager.SaveIndexData(data);
        }
        private static Dictionary<T2, HashSet<T1>> ReverseIndex<T1, T2>(Dictionary<T1, HashSet<T2>> index)
        {
            var reversed = new Dictionary<T2, HashSet<T1>>();

            if (index == null)
                return reversed;

            foreach (var kvp in index)
            {
                T1 key = kvp.Key;
                HashSet<T2> values = kvp.Value;

                if (values == null)
                    continue;

                foreach (T2 value in values)
                {
                    if (value == null || string.IsNullOrEmpty(value.ToString()))
                        continue;

                    if (!reversed.TryGetValue(value, out var set))
                    {
                        set = new HashSet<T1>();
                        reversed[value] = set;
                    }
                    set.Add(key);
                }
            }

            return reversed;
        }

        internal static void ClearIndex()
        {
            IsIndexed = false;
            LastIndexTime = default;
            LabelToAssetIndex.Clear();
            AssetToLabelIndex.Clear();
            PrefabDataList.Clear();
            IndexSavedDataManager.ClearIndexData();

            onIndexUpdate?.Invoke();
        }
    }

    [System.Serializable]
    internal class PrefabData
    {
        public readonly string guid;
        public string assetPath { get; private set; }
        public string name { get; private set; }
        public GameObject prefab;
        public List<string> labels;
        public PrefabData(string guid, List<string> labels)
        {
            this.guid = guid;
            this.assetPath = AssetDatabase.GUIDToAssetPath(guid);
            prefab = AssetDatabase.LoadAssetByGUID<GameObject>(new(guid));
            name = prefab != null ? prefab.name : null;
            this.labels = labels;
        }
        public PrefabData(GameObject prefab, string name, string guid, string path, List<string> labels)
        {
            this.prefab = prefab;
            this.guid = guid;
            this.assetPath = path;
            this.name = name;
            this.labels = labels;
        }

        public void SelectInEditor()
        {
            UnityEditor.Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        /// <summary>
        /// Refreshes the cached path (and, since the underlying asset kept the same name it had
        /// before the move, the display name too) after the asset was moved/renamed on disk.
        /// </summary>
        internal void UpdatePath(string newPath)
        {
            assetPath = newPath;
            name = prefab != null ? prefab.name : System.IO.Path.GetFileNameWithoutExtension(newPath);
        }
    }
}