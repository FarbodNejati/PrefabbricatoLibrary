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
        private static string DEFAULT_SCAN_PATH => PrefabbricatoSettings.LibraryPath;

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


            //Return if already indexed
            if (IsIndexed)
                return;

            //Load data from disk
            var savedData = IndexSavedDataManager.LoadIndexData();



            //Check if we should load this data
            //(index data not null, and has assets indexed in it)
            if (savedData?.labelToAssetIndex!=null)
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
                path = DEFAULT_SCAN_PATH;

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
            //UpdateLabelsFromIndex();

            //Results
            IsIndexed = true;
            LastIndexTime = DateTime.Now;
            onIndexUpdate?.Invoke();
            SaveIndexData();
            //Debug
            Debug.Log($"[Prefabbricato] Scan completed. Indexed {prefab_guids.Count()} assets and {LabelToAssetIndex.Count()} labels.");
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

            IndexSavedDataManager.IndexData data = new(labelAssetIndex, LastIndexTime);
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
            IndexSavedDataManager.ClearIndexData();

            onIndexUpdate?.Invoke();
        }
    }

    internal class PrefabData
    {
        public string guid;
        public string assetPath;
        public string name;
        public GameObject prefab;
        public Texture2D previewThumbnail { get; private set; }

        public void GeneratePreviewThumbnail()
        {
            Texture2D tex = AssetPreview.GetAssetPreview(prefab);

            // AssetPreview works asynchronously; wait until it’s created
            while (tex == null)
            {
                AssetPreview.GetAssetPreview(prefab);
                System.Threading.Thread.Sleep(50);
                tex = AssetPreview.GetMiniThumbnail(prefab);
            }
            previewThumbnail = tex;
        }

        public void SelectInEditor()
        {
            UnityEditor.Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        internal static PrefabData GetPrefabDataFromGUID(string guid)
        {
            var prefab = AssetDatabase.LoadAssetByGUID<GameObject>(new GUID(guid));
            if (prefab == null) return null;

            PrefabData data = new();
            data.prefab = prefab;
            data.name = prefab.name;
            data.guid = guid;
            data.assetPath = AssetDatabase.GetAssetPath(prefab);

            return data;
        }
    }

    public static class TimeSpanExtensions
    {
        public static string ToShortString(this TimeSpan self)
        {
            if(self==null) 
                return null;
            if (self.TotalSeconds < 60)
                return $"{self.Seconds} seconds ago";
            else if (self.TotalMinutes < 60)
                return $"{self.Minutes} minutes ago";
            else if (self.TotalHours < 24)
                return $"{self.Hours} hours ago";
            else if (self.TotalDays < 30)
                return $"{self.Days} days ago";
            else if (self.TotalDays < 90)
                return $"{Mathf.FloorToInt(self.Days / 30)} months ago";
            else
                return $"A long time ago";
        }
    }
}

