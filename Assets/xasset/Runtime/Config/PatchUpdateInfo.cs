using UnityEngine;

namespace xasset
{
    public class PatchUpdateInfo : ScriptableObject
    {
        public static readonly string Filename = "patch_updateinfo.json";
        public string file;
        public string hash;
        public ulong size;
        public long timestamp;
        public int version;
    }
}