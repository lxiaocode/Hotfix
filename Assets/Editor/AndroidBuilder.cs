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

    // TODO 使用生成的脚本无法正常编译打包
    [MenuItem("AndroidBuilder/Step 4: Generate Build Scripts")]
    public static bool GenerateBuildScripts()
    {
        string sdkPath = AndroidExternalToolsSettings.sdkRootPath;
        string jdkPath = AndroidExternalToolsSettings.jdkRootPath;
        StringBuilder allCmd = new StringBuilder();

        string buildToolPath = sdkPath + "/build-tools/" + ANDROID_BUILD_TOOLS_VERSION + "/";

        
        
        // 1. 使用 aapt 工具生成 R.java 文件，该文件用于在 Android 项目中引用资源文件
        string aaptPath = buildToolPath + "/aapt";
        string platformJar = sdkPath + "/platforms/" + ANDROID_PLATFORM + "/android.jar";

        string genRJavaCmd = " package -f -m "                  // -f 强制覆盖已经存在的APK文件 -m 在生成的AndroidManifest.xml文件中包含所有的依赖关系
                             + " -J " + R_JAVA_PATH             // 生成的R.java文件的输出目录
                             + " -M " + MANIFEST_XML_PATH       // AndroidManifest.xml文件的路径
                             + " -S " + RES_PATH                // 资源文件的路径
                             + " -I " + platformJar;            // Android SDK中的android.jar文件的路径，用于编译和链接Android应用程序
        if (!Directory.Exists(R_JAVA_PATH)) { Directory.CreateDirectory(R_JAVA_PATH); }
        allCmd.AppendFormat("{0} {1}\n\n", aaptPath, genRJavaCmd);
        
        
        // 2. 使用 javac 编译 UnityPlayerActivity.java 和 Boostrap.java 文件，并将编译输出到 objs 目录中
        string javacPath = jdkPath + "/bin/javac";
        string[] jarLibFiles = Directory.GetFiles(JAR_LIB_PATH, "*.jar", SearchOption.AllDirectories);
        string[] javaSrcFiles = Directory.GetFiles(ANDROID_PROJECT_PATH, "*.java", SearchOption.AllDirectories);
        string compileParam = " -d " + JAVA_OBJ_PATH                                    // 指定编译输出目录
                                     + " -source 1.8 -target 1.8"                       // 指定源代码和目标代码的版本为Java 8
                                     + " -classpath " + string.Join(";", jarLibFiles)   // 指定类路径，即Unity的Java类库
                                     + " -sourcepath " + JAVA_SRC_PATH                  // 指定源代码的路径
                                     + " -bootclasspath " + platformJar                 // 指定引导类路径，即Android SDK的类库
                                     + " " + String.Join(" ", javaSrcFiles);            // 要编译的Java源文件的路径，这里包括UnityPlayerActivity.java和Boostrap.java两个文件
        if (!Directory.Exists(JAVA_OBJ_PATH)) { Directory.CreateDirectory(JAVA_OBJ_PATH); }
        allCmd.AppendFormat("{0} {1}\n\n", javacPath, compileParam);
        
        
        // 3. 使用 dx 工具将编译输出的 class 文件转换为 dex 文件，并将其保存到 pkg_raw 目录中
        string dxPath = buildToolPath + "/dx";
        string pkgRawPath = BUILD_SCRIPTS_PATH + "/pkg_raw";
        string dexPath = pkgRawPath + "/classes.dex";
        string deParam = " --dex"                                   // 指定将输入的 Java 字节码文件转换成 DEX 格式的命令
                         + " --output=" + dexPath                   // 指定输出的 DEX 文件路径和名称
                         + " " + JAVA_OBJ_PATH                      // 指定包含 Unity 项目编译生成的 Java 代码的目录
                         + " " + string.Join(" ", jarLibFiles);     // 指定包含 Unity 引擎类的 JAR 包路径
        if (!Directory.Exists(pkgRawPath)) { Directory.CreateDirectory(pkgRawPath); }
        allCmd.AppendFormat("{0} {1}\n\nset -x\n\n", dxPath, deParam);



        string outputLibPath = pkgRawPath + "/lib";
        if (Directory.Exists(outputLibPath)) { FileUtil.DeleteFileOrDirectory(outputLibPath); }
        Directory.CreateDirectory(outputLibPath);
        FileUtil.ReplaceDirectory(SO_LIB_PATH + "/arm64-v8a", outputLibPath + "/arm64-v8a");
        FileUtil.DeleteFileOrDirectory(outputLibPath + "/arm64-v8a/Data");
        var debug_files = Directory.GetFiles(outputLibPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".debug") || s.EndsWith(".map") || s.EndsWith(".sym"));
        foreach (string file in debug_files) { File.Delete(file); }



        // 4. 使用 aapt 工具生成未签名的 APK 文件，该文件包含 AndroidManifest.xml、资源文件和 dex 文件
        string binPath = BUILD_SCRIPTS_PATH + "/bin";
        if (!Directory.Exists(binPath)) { Directory.CreateDirectory(binPath); }
        string unaligned_apk_path = binPath + "/" + Application.identifier + ".unaligned.apk";
        string assetsPath = EXPORTED_ASSETS_PATH;
        string buildApkParam = " package -f -m"                     // -f 强制覆盖已经存在的APK文件 -m 在生成的AndroidManifest.xml文件中包含所有的依赖关系
                               + " -F " + unaligned_apk_path        // 指定生成的 APK 文件路径和文件名
                               + " -M " + MANIFEST_XML_PATH         // 指定 Android 应用程序的清单文件路径
                               + " -A " + assetsPath                // 指定 Android 应用程序的 assets 目录路径
                               + " -S " + RES_PATH                  // 指定 Android 应用程序的 res 目录路径
                               + " -I " + platformJar               // 指定 Android 应用程序需要依赖的 Android 平台库文件路径
                               + " " + pkgRawPath;                  // 指定需要打包的资源文件路径
        allCmd.AppendFormat("{0} {1}\n\n", aaptPath, buildApkParam);

        
        
        // 5. 使用 zipalign 工具对未签名的 APK 文件进行优化
        string zipalign = buildToolPath + "/zipalign";
        string alignedApkName = Application.identifier + ".apk";
        string alignedApkPath = binPath + "/" + alignedApkName;
        // -f 强制执行，如果目标文件已经存在，覆盖它
        // 4 对齐字节大小，必须是2的幂次方，这里是4
        // 需要优化的APK文件路径和名称
        // 优化后的APK文件路径和名称
        string zipalignParam = " -f 4 " + unaligned_apk_path + " " + alignedApkPath;
        allCmd.AppendFormat("{0} {1}\n\n", zipalign, zipalignParam);
        
        
        
        
        // 6. 使用 apksigner 工具对优化后的 APK 文件进行签名
        string keystoreDir = PROJECT_DIR + "/AndroidKeystore";
        if (!Directory.Exists(keystoreDir)) { Directory.CreateDirectory(keystoreDir); }
        string keystoreFile = keystoreDir + "/test.keystore";
        if (!File.Exists(keystoreFile))
        {
            string keytoolPath = jdkPath + "/bin/keytool";
            string genKeyParam = "-genkey -alias test -validity 1000 -keyalg RSA -keystore " + keystoreFile + " -dname \"CN = Test, OU = Test, O = Test, L = Test, S = Test, C = Test\" -keysize 4096 -storepass testtest -keypass testtest";
            if (!Exec(keytoolPath, genKeyParam))
            {
                Debug.LogError("exec failed:" + keytoolPath + " " + genKeyParam);
                return false;
            }
        }
        string apksignerPath = buildToolPath + "/apksigner";
        string signParam = " sign --ks " + keystoreFile + " --ks-pass pass:testtest --key-pass pass:testtest " + alignedApkPath;
        allCmd.AppendFormat("{0} {1}\n\n", apksignerPath, signParam);
        
        
        
        
        //del tmp apk
        allCmd.AppendFormat("rm -f {0}\n\n", unaligned_apk_path.Replace("/", "\\"));
        allCmd.AppendFormat("set -x\n\n"); //explorer as the last line wont return success, so...
        File.WriteAllText(BUILD_SCRIPTS_PATH + "/build_apk.sh", allCmd.ToString());

        if (!Exec("chmod", " 755 " + BUILD_SCRIPTS_PATH + "/build_apk.sh"))
        {
            Debug.LogError("exec failed: chmod build_apk.sh");
            return false; 
        }
        
        
        return true;
    }

    [MenuItem("AndroidBuilder/Step 5: Build Apk File")]
    public static bool BuildApk()
    {
        string buildApkPath = BUILD_SCRIPTS_PATH + "/build_apk.sh";
        string alignedApkName = Application.identifier + ".apk";
        string alignedApkPath = BUILD_SCRIPTS_PATH + "/bin/" + alignedApkName;

        if (!Exec(buildApkPath, ""))
        {
            Debug.LogError("exec failed:" + buildApkPath);
            return false;
        }

        if (!File.Exists(alignedApkPath))
        {
            Debug.LogError("apk not found:" + alignedApkPath + ", exec failed:" + buildApkPath);
            return false;
        }
        return true;
    }
}
