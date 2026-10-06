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

            [System.Serializable]
            public class TokenToAssetIndexEntry
            {
                public string token;
                public List<string> guids;
            }

            //Saved fields
            [SerializeField]
            private int schemaVersion = 1;

            [SerializeField]
            public string indexPath;

            [SerializeField]
            private string lastIndexBuildTime;

            // Serialized fields for JSON
            public List<LabelToAssetIndexEntry> entries = new();
            public List<TokenToAssetIndexEntry> tokenEntries = new();

            // Runtime dictionaries for fast lookups
            [NonSerialized]
            public Dictionary<string, List<string>> labelToAssetIndex = new();

            [NonSerialized]
            public Dictionary<string, List<string>> tokenToAssetIndex = new();

            /// <summary>
            /// False for index files created before token indexing was persisted.
            /// </summary>
            public bool HasTokenIndex => schemaVersion >= 1;

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

            public IndexData(
                Dictionary<string, List<string>> labelToAssetIndex,
                Dictionary<string, List<string>> tokenToAssetIndex,
                DateTime lastIndexBuildTime,
                string indexPath)
            {
                schemaVersion = 1;
                this.labelToAssetIndex = labelToAssetIndex;
                this.tokenToAssetIndex = tokenToAssetIndex;
                LastIndexBuildTime = lastIndexBuildTime;
                this.indexPath = indexPath;
            }

            // Convert dictionary to serializable format
            public void PrepareForSerialization()
            {
                entries.Clear();
                foreach (var kvp in labelToAssetIndex)
                {
                    entries.Add(new LabelToAssetIndexEntry { label = kvp.Key, guids = kvp.Value });
                }

                tokenEntries.Clear();
                foreach (var kvp in tokenToAssetIndex)
                {
                    tokenEntries.Add(new TokenToAssetIndexEntry { token = kvp.Key, guids = kvp.Value });
                }
            }

            // Convert back to dictionary after deserialization
            public void PrepareForRuntime()
            {
                labelToAssetIndex = new();
                foreach (var entry in entries ?? new List<LabelToAssetIndexEntry>())
                {
                    if (!string.IsNullOrEmpty(entry.label))
                        labelToAssetIndex[entry.label] = entry.guids ?? new List<string>();
                }

                tokenToAssetIndex = new();
                foreach (var entry in tokenEntries ?? new List<TokenToAssetIndexEntry>())
                {
                    if (!string.IsNullOrEmpty(entry.token))
                        tokenToAssetIndex[entry.token] = entry.guids ?? new List<string>();
                }
            }



        }
        public static void SaveIndexData(IndexData data)
        {
            data.PrepareForSerialization();

            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(INDEX_DATA_PATH, json);
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
