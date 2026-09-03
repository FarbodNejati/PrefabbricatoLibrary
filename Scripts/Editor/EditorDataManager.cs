using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Analytics.IAnalytic;

namespace Farbod.Prefabbricato
{
    internal static class EditorDataManager
    {
        private static string USER_DATA_PATH = Application.dataPath + "/../Library/Prefabbricato_UserData.json";
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


        ///----------------------------------------------
        ///------------  USER DATA / PREFS  -------------
        ///----------------------------------------------

        [System.Serializable]
        public class UserData
        {
            public string libraryPath;
            public List<LabelData> labelUserData = new();
        }
        [System.Serializable]
        public class LabelData
        {
            public string name; //The name of this asset label
            public Color? color; //The color assigned by the user to this label
            public int latestCount;

            public LabelData(string name, Color? color, int latestCount=0)
            {
                this.name = name;
                this.color = color;
                this.latestCount = latestCount;
            }
        }


        private static UserData cachedData;
        // Load data (with caching for performance)
        private static UserData LoadData()
        {
            if (cachedData != null)
                return cachedData;

            if (File.Exists(USER_DATA_PATH))
            {
                var json = File.ReadAllText(USER_DATA_PATH);
                cachedData = JsonUtility.FromJson<UserData>(json);

                // Ensure labelUserData is not null
                if (cachedData?.labelUserData == null)
                    cachedData.labelUserData = new List<LabelData>();
            }
            else
            {
                // Create new data if file doesn't exist
                cachedData = new UserData
                {
                    libraryPath = string.Empty,
                    labelUserData = new List<LabelData>()
                };
            }

            return cachedData;
        }

        // Save entire data to file
        private static void SaveData()
        {
            if (cachedData == null)
                return;

            var json = JsonUtility.ToJson(cachedData, true);
            File.WriteAllText(USER_DATA_PATH, json);
            AssetDatabase.Refresh();
        }

        // Public method to get the current library path
        public static string GetLibraryPath()
        {
            var data = LoadData();
            return data?.libraryPath ?? string.Empty;
        }

        // Public method to update ONLY the library path
        public static void SaveLibraryPath(string path)
        {
            var data = LoadData();
            data.libraryPath = path;
            SaveData();
        }

        // Public method to add/update a label color
        public static void SaveLabelColor(string labelName, Color color)
        {
            var data = LoadData();

            // Find existing entry
            var existing = data.labelUserData.Find(x => x.name == labelName);
            if (existing != null)
            {
                existing.color = color;
            }
            else
            {
                data.labelUserData.Add(new LabelData(labelName,color));
            }

            SaveData();
        }

        // Public method to get color for a label
        public static Color GetLabelColor(string labelName)
        {
            var data = LoadData();
            var entry = data.labelUserData.Find(x => x.name == labelName);
            return entry?.color ?? Color.white; // Default color if not found
        }

        // Public method to get all label colors as a dictionary
        public static Dictionary<string, Color?> GetAllLabelColors()
        {
            var data = LoadData();
            var result = new Dictionary<string, Color?>();
            foreach (var entry in data.labelUserData)
            {
                result[entry.name] = entry.color;
            }
            return result;
        }

        // Clear cache (useful for testing or if you want to force reload)
        public static void ClearCache()
        {
            cachedData = null;
        }
    }
}
