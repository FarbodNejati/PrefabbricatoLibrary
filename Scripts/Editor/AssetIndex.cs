using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Farbod.Prefabbricato.EditorDataManager;

namespace Farbod.Prefabbricato.Backend
{
    internal static class AssetIndex
    {
        private static string DefaultScanPath => PrefabbricatoSettings.LibraryPath;
        /// <summary>
        /// Has our index been built at least once?
        /// </summary>
        internal static bool IsIndexed { get; private set; }
        internal static DateTime LastIndexTime { get; private set; }
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
        internal static Dictionary<string, HashSet<string>> AssetGUIDToAssetDataIndex { get; private set; } = new();

        internal static List<LabelData> Labels { get; private set; } = new();
        internal static event Action OnIndexUpdate;

        [InitializeOnLoadMethod]
        static void LoadIndexData()
        {
            //Subscribe to app quit for saving idex data
            EditorApplication.quitting += () => SaveIndexData();


            //Return if already indexed
            if (IsIndexed)
                return;

            //Load data from disk
            var savedData = EditorDataManager.LoadIndexData();

            //Check if we should load this data
            //(index data not null, and has assets indexed in it)
            if(savedData?.labelToAssetIndex?.Count>0)
            {
                IsIndexed = true;

                //Load last index time
                LastIndexTime = DateTime.Now;

                //Load label to asset guid index
                LabelToAssetIndex = new();
                foreach (var kvp in savedData.labelToAssetIndex)
                {
                    // Convert List<string> to HashSet<string>
                    LabelToAssetIndex[kvp.Key] = new HashSet<string>(kvp.Value);
                }

                //Build asset guid to label index
                AssetToLabelIndex = ReverseIndex(LabelToAssetIndex);
                UpdateLabelsFromIndex();
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
                path = DefaultScanPath;

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
            UpdateLabelsFromIndex();

            //Results
            IsIndexed = true;
            LastIndexTime = DateTime.Now;
            OnIndexUpdate?.Invoke();
            SaveIndexData();
            //Debug
            Debug.Log($"[Prefabbricato] Scan completed. Indexed {prefab_guids.Count()} assets and {LabelToAssetIndex.Count()} labels.");
        }
        private static void UpdateLabelsFromIndex()
        {
            var labelColors = EditorDataManager.GetAllLabelColors();
            Labels.Clear();

            LabelData[] data = new LabelData[LabelToAssetIndex.Count];
            int i = 0;
            foreach (var item in LabelToAssetIndex)
            {
                string labelName = item.Key;
                bool hasSavedColor = labelColors.TryGetValue(labelName, out var color);
                data[i] = new(labelName, hasSavedColor?color:null, item.Value?.Count??0);
                i++;
            }

            Array.Sort(data, (a, b) => b.latestCount.CompareTo(a.latestCount));
            Labels.AddRange(data);
        }
        private static void SaveIndexData()
        {
            if (!IsIndexed)
                return;
            
            Dictionary<string, List<string>> labelAssetIndex = new();
            foreach (string label in LabelToAssetIndex.Keys.ToArray())
            {
                labelAssetIndex[label] = LabelToAssetIndex[label].ToList();
            }

            EditorDataManager.IndexData data = new(labelAssetIndex, LastIndexTime);
            EditorDataManager.SaveIndexData(data);
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
}

