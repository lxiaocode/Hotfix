using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class Bootstrap
{
#if UNITY_ANDROID && !UNITY_EDITOR
    [DllImport("bootstrap")]
    public static extern string get_arch_abi();

    [DllImport("bootstrap")]
    public static extern string use_data_dir(string _data_path, string _apk_path);
#else
    public static string get_arch_abi() { return "armeabi-v7a"; }
    public static string use_data_dir(string _data_path, string _apk_path) { return ""; }
#endif
}
