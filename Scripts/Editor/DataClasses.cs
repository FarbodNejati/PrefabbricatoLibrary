using System;
using UnityEngine;
using System.Collections.Generic;

namespace Farbod.Prefabbricato
{
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
        public int latestCount;
        [NonSerialized]
        public UnityEngine.Color? color;


        // Helper fields for serialization
        [SerializeField]
        private string colorHex;
        [SerializeField]
        private bool hasColor;

        public LabelData(string name, Color? color, int latestCount = 0)
        {
            this.name = name;
            this.color = color;
            this.latestCount = latestCount;
            SerializeColorField();
        }

        // Called before serialization
        public void SerializeColorField()
        {
            hasColor = color.HasValue;
            colorHex = hasColor ? ColorUtility.ToHtmlStringRGBA(color.Value) : "";
        }

        // Called after deserialization
        public void DeserializeColorField()
        {
            if (hasColor && !string.IsNullOrEmpty(colorHex))
            {
                if (ColorUtility.TryParseHtmlString($"#{colorHex}", out Color parsedColor))
                {
                    color = parsedColor;
                }
                else
                {
                    color = null;
                }
            }
            else
            {
                color = null;
            }
        }
    }
}
