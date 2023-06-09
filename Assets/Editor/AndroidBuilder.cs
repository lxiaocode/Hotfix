using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;
using xasset;

public class AndroidBuilder : MonoBehaviour
{
    public static readonly string ANDROID_BUILD_TOOLS_VERSION = "30.0.2";
    public static readonly string ANDROID_PLATFORM = "android-29";
    
    
    public static readonly string PROJECT_DIR = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
    // ANDROID PROJECT
    public static readonly string ANDROID_EXPORT_PATH = PROJECT_DIR + "AndroidGradleProject";
    public static string ANDROID_PROJECT_PATH = ANDROID_EXPORT_PATH + "/unityLibrary";

    public static string JAR_LIB_PATH = ANDROID_PROJECT_PATH + "/libs/";
    
    public static string BUILD_SCRIPTS_PATH = ANDROID_PROJECT_PATH + "/src/main/";

    public static string JAVA_OBJ_PATH = ANDROID_PROJECT_PATH + "/src/main/objs/";
    public static string JAVA_SRC_PATH = ANDROID_PROJECT_PATH + "/src/main/java/";
    public static string R_JAVA_PATH = ANDROID_PROJECT_PATH + "/src/main/gen/";
    public static string RES_PATH = ANDROID_PROJECT_PATH + "/src/main/res/";
    public static string EXPORTED_ASSETS_PATH = ANDROID_PROJECT_PATH + "/src/main/assets/";
    public static string SO_LIB_PATH = ANDROID_PROJECT_PATH + "/src/main/jniLibs/";
    
    public static string MANIFEST_XML_PATH = ANDROID_PROJECT_PATH + "/src/main/AndroidManifest.xml";
    
    
    public static string SO_DIR_NAME = "jniLibs";
    
    public static string ZIP_PATH = "zip";
    
    // TODO 执行shell脚本时 exit_code=255
    static bool Exec(string filename, string args)
    {
        System.Diagnostics.Process process = new System.Diagnostics.Process();
        process.StartInfo.FileName = filename;
        process.StartInfo.Arguments = args;

        int exit_code = -1;

        try
        {
            process.Start();
            if (process.StartInfo.RedirectStandardOutput && process.StartInfo.RedirectStandardError)
            {
                process.BeginOutputReadLine();
                Debug.LogError(process.StandardError.ReadToEnd());
            }
            else if (process.StartInfo.RedirectStandardOutput)
            {
                string data = process.StandardOutput.ReadToEnd();
                Debug.Log(data);
            }
            else if (process.StartInfo.RedirectStandardError)
            {
                string data = process.StandardError.ReadToEnd();
                Debug.LogError(data);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
        process.WaitForExit();
        exit_code = process.ExitCode;
        process.Close();
        return exit_code == 0;
    }
    
    public static bool ValidateConfig()
    {
        string sdkPath = AndroidExternalToolsSettings.sdkRootPath;
        if (string.IsNullOrEmpty(sdkPath))
        {
            Debug.LogError("sdk path is empty! please config via menu path:Edit/Preference->External tools.");
            return false;
        }

        string jdkPath = AndroidExternalToolsSettings.jdkRootPath;
        if (string.IsNullOrEmpty(jdkPath))
        {
            Debug.LogError("jdk path is empty! please config via menu path:Edit/Preference->External tools.");
            return false;
        }

        string ndkPath = AndroidExternalToolsSettings.ndkRootPath;
        if (string.IsNullOrEmpty(ndkPath))
        {
            Debug.LogError("ndk path is empty! please config via menu path:Edit/Preference->External tools.");
            return false;
        }

        string buildToolPath = sdkPath + "/build-tools/" + ANDROID_BUILD_TOOLS_VERSION + "/";
        if (!Directory.Exists(buildToolPath))
        {
            Debug.LogError("Android Build Tools not found. Try to reconfig version on the top of AndroidBuilder.cs. In Unity2018, it can't be work if less than 26.0.2. current:" + buildToolPath);
            return false;
        }

        string platformJar = sdkPath + "/platforms/" + ANDROID_PLATFORM + "/android.jar";
        if (!File.Exists(platformJar))
        {
            Debug.LogError("Android Platform not found. Try to reconfig version on the top of AndroidBuilder.cs. current:" + platformJar);
            return false;
        }

        Debug.Log("Build Env is ready!");
        Debug.Log("Build Options:");
        Debug.Log("SDK PATH=" + sdkPath);
        Debug.Log("JDK PATH=" + jdkPath);
        Debug.Log("BUILD TOOLS PATH=" + buildToolPath);
        Debug.Log("ANDROID PLATFORM=" + platformJar);
        return true;
    }
    
    [MenuItem("AndroidBuilder/Step 1: ExportGradleProject")]
    public static bool ExportGradleProject()
    {
        if (!ValidateConfig()) return false;
        
        // PlayerSettings
        PlayerSettings.applicationIdentifier = "com.lxiaocode.hot";
        PlayerSettings.companyName = "lxiaocode";
        PlayerSettings.productName = "hot";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        
        // EditorUserBuildSettings
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        // 导出 Android Gradle 项目
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        
        // export
        if (Directory.Exists(ANDROID_EXPORT_PATH)) { FileUtil.DeleteFileOrDirectory(ANDROID_EXPORT_PATH);}
        Directory.CreateDirectory(ANDROID_EXPORT_PATH);
        string error_msg = String.Empty;
        try
        {
            error_msg = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, ANDROID_EXPORT_PATH, BuildTarget.Android, BuildOptions.None).summary.result == BuildResult.Succeeded ? String.Empty : "Failed to export project!";
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
            return false;
        }

        if (!string.IsNullOrEmpty(error_msg))
        {
            Debug.LogError(error_msg);
            return false;
        }

        // 2020 以上版本导出项目不再生成 libil2cpp.so 文件，需要自己编译
        string il2cppPath = BUILD_SCRIPTS_PATH + "/Il2CppOutputProject/IL2CPP/build/deploy/il2cpp";
        string outputPath = SO_LIB_PATH + "/arm64-v8a/libil2cpp.so";
        string baseLibPath = BUILD_SCRIPTS_PATH + "/jniStaticLibs/arm64-v8a";
        string generatedCppPath = BUILD_SCRIPTS_PATH + "/Il2CppOutputProject/Source/il2cppOutput";
        string cachePath = ANDROID_PROJECT_PATH + "/build/il2cpp_arm64-v8a_Release/il2cpp_cache";
        string ndkPath = AndroidExternalToolsSettings.ndkRootPath;
        string buildIl2cppParam = " --compile-cpp"                              // 编译C++代码
                                  + " --platform=Android"                       // 编译Android平台的代码
                                  + " --architecture=arm64"                     // 编译64位ARM架构的代码
                                  + " --outputpath=" + outputPath               // 指定输出路径
                                  + " --libil2cpp-static"                       // 使用静态库编译IL2CPP代码
                                  + " --baselib-directory=" + baseLibPath       // 指定静态库所在目录
                                  + " --incremental-g-c-time-slice=3"           // 设置增量垃圾回收的时间片大小为3
                                  + " --configuration=Release"                  // 设置编译模式为Release
                                  + " --dotnetprofile=unityaot-linux"           // 指定.NET运行时的配置文件
                                  + " --print-command-line"                     // 打印编译命令行
                                  + " --generatedcppdir=" + generatedCppPath    // 指定生成的C++代码的目录
                                  + " --cachedirectory=" + cachePath            // 指定IL2CPP缓存目录
                                  + " --tool-chain-path=" + ndkPath;            // 指定NDK的路径
        if (!Exec(il2cppPath, buildIl2cppParam))
        {
            Debug.LogError("exec failed:" + il2cppPath + " " + buildIl2cppParam);
            return false;
        }
        
        return true;

    }

    [MenuItem("AndroidBuilder/Step 3: Generate Bin Patches")]
    public static bool GenerateBinPatches()
    {
        string assetBinDataPath = EXPORTED_ASSETS_PATH + "bin/Data/";
        
        string patchTopPath = PROJECT_DIR + "/AllAndroidPatchFiles/";
        string assertBinDataPatchPath = patchTopPath + "/assets_bin_Data/";
        
        if (Directory.Exists(patchTopPath)) { FileUtil.DeleteFileOrDirectory(patchTopPath); }
        Directory.CreateDirectory(assertBinDataPatchPath);

        string[][] soPatchFile =
        {
            // path_in_android_project, filename inside zip, zip file name
            new string[3]{ "/"+ SO_DIR_NAME + "/arm64-v8a/libil2cpp.so", "libil2cpp.so.new", "lib_arm64-v8a_libil2cpp.so.zip" },
        };
        
        for (int i = 0; i < soPatchFile.Length; i++)
        {
            string[] specialPaths = soPatchFile[i];
            string projectRelativePath = specialPaths[0];
            string pathInZipFile = specialPaths[1];
            string zipFileName = specialPaths[2];

            string projectFullPath = BUILD_SCRIPTS_PATH + projectRelativePath;
            ZipHelper.ZipFile(projectFullPath, pathInZipFile, patchTopPath + zipFileName, 9);
        }
        
        string[] allAssetsBinDataFiles = Directory.GetFiles(assetBinDataPath, "*", SearchOption.AllDirectories);
        StringBuilder allZipCmds = new StringBuilder();
        allZipCmds.AppendFormat("if [ ! -d \"{0}\" ]; then mkdir {0}; fi\n", patchTopPath);
        allZipCmds.AppendFormat("if [ ! -d \"{0}\" ]; then mkdir {0}; fi\n", assertBinDataPatchPath);
        foreach (string apk_file in allAssetsBinDataFiles)
        {
            string relativePathHeader = "assets/bin/Data/";
            int relativePathStart = apk_file.IndexOf(relativePathHeader);
            string filenameInZip = apk_file.Substring(relativePathStart);                                                //file: assets/bin/Data/xxx/xxx
            string relativePath = apk_file.Substring(relativePathStart + relativePathHeader.Length).Replace('\\', '/'); //file: xxx/xxx
            string zipFileName = relativePath.Replace("/", "__").Replace("\\", "__") + ".bin";                                     //file: xxx__xxx.bin

            allZipCmds.AppendFormat("cd {0} && {1} -8 \"{2}\" \"{3}\"\n", BUILD_SCRIPTS_PATH, ZIP_PATH, assertBinDataPatchPath + zipFileName, filenameInZip);
        }
        allZipCmds.Append("sleep 1\n");
        allZipCmds.AppendFormat("cd {0} && {1} -9 -r \"{2}\" {3}\n", patchTopPath, ZIP_PATH, PROJECT_DIR + "/AllAndroidPatchFiles_Version1.zip", ".");
        
        if (allZipCmds.Length > 0)
        {
            string zipPatchesFile = BUILD_SCRIPTS_PATH + "/" + "zip_patches.sh";
            File.WriteAllText(zipPatchesFile, allZipCmds.ToString());
            
            if (!Exec("chmod", " 755 " + BUILD_SCRIPTS_PATH + "/zip_patches.sh"))
            {
                Debug.LogError("exec failed: chmod zip_patches.sh");
                return false; 
            }
        }
        
        return true;
    }

    [MenuItem("Build/Build Patch")]
    public static bool BuildPatch()
    {
        ExportGradleProject();
        GenerateBinPatches();
        
        // patch_updateinfo.json
        string[] patchUpdateInfoFiles = Directory.GetFiles(PROJECT_DIR + "/build", "patch_updateinfo.json", SearchOption.AllDirectories);
        if (patchUpdateInfoFiles.Length > 0)
        {
            string patchUpdateInfoPath = patchUpdateInfoFiles[0];
            var patchUpdateInfo = Utility.LoadFromFile<PatchUpdateInfo>(patchUpdateInfoPath);
            // TODO 更新其他信息
            patchUpdateInfo.version += 1;
            File.WriteAllText(patchUpdateInfoPath, JsonUtility.ToJson(patchUpdateInfo));

            return true;
        }

        return false;
    }
}
