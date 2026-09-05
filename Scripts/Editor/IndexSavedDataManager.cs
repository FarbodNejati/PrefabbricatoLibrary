using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farbod.Prefabbricato.Backend
{
    /// <summary>
    /// This class saves/loads the index from disk
    /// to avoid rebuilding the index on every domain reload.
    /// </summary>
    internal static class IndexSavedDataManager
    {
        private static string INDEX_DATA_PATH = Application.dataPath + "/../Library/Prefabbricato_AssetIndex.json";


        ///----------------------------------------------
        ///------------  ASSET INDEX DATA  --------------
        ///----------------------------------------------
        [System.Serializable]
        public class IndexData
        {
            [System.Serializable]
            public class LabelToAssetIndexEntry
            {
                public string label;
                public List<string> guids;
            }

            //Saved fields
            [SerializeField]
            private string lastIndexBuildTime;

            // Serialized field for JSON
            public List<LabelToAssetIndexEntry> entries = new();

            // Runtime dictionary for fast lookups
            [NonSerialized]
            public Dictionary<string, List<string>> labelToAssetIndex = new();

            //Date time
            private static readonly IFormatProvider dateFormatProvider = CultureInfo.InvariantCulture;
            public DateTime LastIndexBuildTime
            {
                get
                {
                    if (string.IsNullOrEmpty(lastIndexBuildTime))
                        return DateTime.MinValue;
                    //Parse
                    if (DateTime.TryParse(lastIndexBuildTime, dateFormatProvider, DateTimeStyles.None, out var result))
                        return result;

                    return DateTime.MinValue;
                }
                set
                {
                    lastIndexBuildTime = value.ToString(dateFormatProvider);
                }
            }

            public IndexData(Dictionary<string, List<string>> labelToAssetIndex, DateTime lastIndexBuildTime)
            {
                this.labelToAssetIndex = labelToAssetIndex;
                LastIndexBuildTime = lastIndexBuildTime;
            }

            // Convert dictionary to serializable format
            public void PrepareForSerialization()
            {
                entries.Clear();
                foreach (var kvp in labelToAssetIndex)
                {
                    entries.Add(new LabelToAssetIndexEntry { label = kvp.Key, guids = kvp.Value });
                }
            }

            // Convert back to dictionary after deserialization
            public void PrepareForRuntime()
            {
                labelToAssetIndex = new();
                foreach (var entry in entries)
                {
                    labelToAssetIndex[entry.label] = entry.guids;
                }
            }


            
        }
        public static void SaveIndexData(IndexData data)
        {
            data.PrepareForSerialization();

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(INDEX_DATA_PATH, json);
            AssetDatabase.Refresh();
        }
        public static IndexData LoadIndexData()
        {
            if (!File.Exists(INDEX_DATA_PATH)) return null;
            var json = File.ReadAllText(INDEX_DATA_PATH);
            var data = JsonUtility.FromJson<IndexData>(json);
            data?.PrepareForRuntime();
            return data;
        }
        public static void ClearIndexData()
        {
            if (File.Exists(INDEX_DATA_PATH))
                File.WriteAllText(INDEX_DATA_PATH, "");
        }
    }
}
