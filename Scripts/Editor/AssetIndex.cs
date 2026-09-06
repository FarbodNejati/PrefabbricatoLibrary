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
        internal static bool IsStale => IsIndexed?LastIndexSpan.TotalDays>INDEX_STALE_THRESHOLD_DAYS: false;

        /// <summary>
        /// Has our index been built at least once?
        /// </summary>
        internal static bool IsIndexed { get; private set; }

        internal static DateTime m_LastIndexTime;
        internal static DateTime LastIndexTime {
            get => m_LastIndexTime;
            set {
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

        internal static event Action onIndexUpdate;

        [InitializeOnLoadMethod]
        static void LoadIndexData()
        {
            //Subscribe to app quit for saving idex data
            EditorApplication.quitting += () => SaveIndexData();

            PrefabbricatoSettings.onLibraryChange += (newPath) =>
            {
                if(m_LastIndexPath!=newPath)
                    IsIndexed = false;
            };

            //Return if already indexed
            if (IsIndexed)
                return;

            //Load data from disk
            var savedData = IndexSavedDataManager.LoadIndexData();



            //Check if we should load this data
            //(index data not null, and path matches)
            if (savedData?.labelToAssetIndex!=null && savedData.indexPath== SCAN_PATH)
            {
                IsIndexed = true;

                //Load last index time
                LastIndexTime = savedData.LastIndexBuildTime;

                //Load label to asset guid index
                LabelToAssetIndex = new();
                foreach (var kvp in savedData.labelToAssetIndex)
                {
                    // Convert List<string> to HashSet<string>
                    LabelToAssetIndex[kvp.Key] = new HashSet<string>(kvp.Value);
                }

                //Build asset guid to label index
                AssetToLabelIndex = ReverseIndex(LabelToAssetIndex);
                BuildPrefabDataList();
                //UpdateLabelsFromIndex();
                //OnIndexUpdate?.Invoke();
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
                var prefab = AssetDatabase.LoadAssetByGUID<GameObject>(new GUID(guid));
                PrefabData data = new(prefab, guid, prefab.name, AssetToLabelIndex[guid].ToList());
                PrefabDataList.Add(data);
            }
        }

        //private static void UpdateLabelsFromIndex()
        //{
        //    var labelColors = EditorDataManager.GetAllAssignedLabelColors();

        //    LabelData[] data = new LabelData[LabelToAssetIndex.Count];
        //    int i = 0;
        //    foreach (var item in LabelToAssetIndex)
        //    {
        //        string labelName = item.Key;
        //        bool hasSavedColor = labelColors.TryGetValue(labelName, out var color);
        //        data[i] = new(labelName, hasSavedColor?color:null, item.Value?.Count??0);
        //        i++;
        //    }

        //    Array.Sort(data, (a, b) => b.latestCount.CompareTo(a.latestCount));
        //    Labels.AddRange(data);
        //}
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
    internal struct PrefabData
    {
        public readonly string guid;
        public readonly string assetPath;
        public readonly string name;
        public GameObject prefab;
        public List<string> labels;

        public PrefabData(GameObject prefab, string guid, string path, List<string> labels)
        {
            this.prefab = prefab;
            this.guid = guid;
            this.assetPath = path;
            this.name = prefab.name;
            this.labels = labels;
        }
        
        public void SelectInEditor()
        {
            UnityEditor.Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}

