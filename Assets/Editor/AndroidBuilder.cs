using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class AndroidBuilder : MonoBehaviour
{
    public static readonly string ANDROID_BUILD_TOOLS_VERSION = "30.0.2";
    public static readonly string ANDROID_PLATFORM = "android-29";
    
    
    public static readonly string PROJECT_DIR = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
    // ANDROID PROJECT
    public static readonly string ANDROID_EXPORT_PATH = PROJECT_DIR + "AndroidGradleProject";
    public static string JAVA_SRC_PATH = ANDROID_EXPORT_PATH + "/unityLibrary/src/main/java/";
    
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

        return true;

    }

    [MenuItem("AndroidBuilder/Step 2: Patch Gradle Project")]
    public static bool PatchAndroidProject()
    {
        string[] javaEntranceFiles = Directory.GetFiles(JAVA_SRC_PATH, "UnityPlayerActivity.java", SearchOption.AllDirectories);
        if (javaEntranceFiles.Length != 1)
        {
            Debug.LogError("UnityPlayerActivity.java not found or more than one.");
            return false;
        }
        string javaEntranceFile = javaEntranceFiles[0];
        string allJavaText = File.ReadAllText(javaEntranceFile);
        if (allJavaText.IndexOf("bootstrap", StringComparison.Ordinal) > 0)
        {
            Debug.Log("UnityPlayerActivity.java already patched.");
            return true;
        }
        allJavaText = allJavaText.Replace("import android.view.WindowManager;",
            @"import android.view.WindowManager;
import io.github.noodle1983.Boostrap;");

        allJavaText = allJavaText.Replace("mUnityPlayer = new UnityPlayer(this, this);",
            @"Boostrap.InitNativeLibBeforeUnityPlay(getApplication().getApplicationContext().getFilesDir().getPath());
        mUnityPlayer = new UnityPlayer(this, this);");
        File.WriteAllText(javaEntranceFile, allJavaText);

        return true;
    }
}
