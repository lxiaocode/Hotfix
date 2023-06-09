using System.Collections.Generic;
using UnityEngine;

namespace xasset
{
    public enum PlayerAssetsSplitMode
    {
        SplitByAssetPacksWithInstallTime,
        IncludeAllAssets,
        ExcludeAllAssets
    }

    public class PlayerAssets : ScriptableObject
    {
        public static readonly string Filename = $"{nameof(PlayerAssets).ToLower()}.json";
        public List<string> data = new List<string>();
        public LogLevel logLevel = LogLevel.Debug;
        public PlayerAssetsSplitMode splitMode = PlayerAssetsSplitMode.ExcludeAllAssets;
        public byte maxDownloads = 5;
        public byte maxRetryTimes = 3;
        public bool updatable;
        public bool packed;
        public string version;
        public string updateInfoURL;
        public string downloadURL;
        public byte maxRequests;
        public bool autoSlicing;
        public float autoSliceTimestep;
        public float autoRecycleTimestep;
        // il2cpp patch
        // 母包版本
        public int patchVersion;
        // 服务器更新信息URL
        public string patchUpdateInfoURL;

        public bool Contains(string key)
        {
            return data.Contains(key);
        }
    }
}