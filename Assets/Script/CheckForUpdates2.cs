using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace xasset.samples
{
    public class CheckForUpdates2 : MonoBehaviour
    {
        public Text version;
        public UnityEvent completed;
        private DownloadRequestBase _downloadAsync;
        private Versions versions;

        private int updateVersion = 0;
        public static string RUNTIME_PATCH_PATH_FORMAT { get { return Application.persistentDataPath + "/Version_{0}"; } }

        void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void OnCheckUpdate()
        {
            StartCoroutine(CheckUpdate());
        }
        
        private IEnumerator CheckUpdate()
        {
            var initializeAsync = Assets.InitializeAsync();
            yield return initializeAsync;
            UpdateVersion();
            // 预加载
            yield return MessageBox.LoadAsync();
            yield return LoadingScreen.LoadAsync(); 
            // 获取更新信息
            var getUpdateInfoAsync = Assets.GetUpdateInfoAsync();
            yield return getUpdateInfoAsync;
            if (getUpdateInfoAsync.result == Request.Result.Success)
            {
                var updateVersion = System.Version.Parse(getUpdateInfoAsync.info.version);
                var playerVersion = System.Version.Parse(Assets.PlayerAssets.version);
                if (updateVersion.Major != playerVersion.Major ||
                    updateVersion.Minor > playerVersion.Minor) // 需要强更下载安装包。
                {
                    var request = MessageBox.Show(Constants.Text.Tips,
                        string.Format(Constants.Text.TipsNewContent, updateVersion));
                    yield return request;
                    if (request.result == Request.Result.Success)
                        Application.OpenURL(getUpdateInfoAsync.info.playerURL);
                    Quit();
                    yield break;
                }

                var getVersionsAsync = Assets.GetVersionsAsync(getUpdateInfoAsync.info);
                while (!getVersionsAsync.isDone)
                {
                    var msg = $"{Constants.Text.UpdateVersions} {getVersionsAsync.progress * 100:f2}%";
                    LoadingScreen.Instance.SetProgress(msg, getVersionsAsync.progress);
                    yield return null;
                }

                versions = getVersionsAsync.versions;
                var getDownloadSizeAsync = Assets.GetDownloadSizeAsync(versions);
                yield return getDownloadSizeAsync;
                if (getDownloadSizeAsync.downloadSize > 0)
                {
                    var downloadSize = Utility.FormatBytes(getDownloadSizeAsync.downloadSize);
                    var message = string.Format(Constants.Text.TipsNewContent, downloadSize);
                    var update = MessageBox.Show(Constants.Text.Tips, message);
                    yield return update;
                    if (update.result == Request.Result.Success)
                    {
                        _downloadAsync = getDownloadSizeAsync.DownloadAsync();
                        yield return Downloading();
                        if (_downloadAsync.result == DownloadRequestBase.Result.Success)
                        {
                            // 清理历史文件
                            yield return Clearing();
                            var reload = Assets.ReloadAsync(versions);
                            while (!reload.isDone)
                            {
                                var msg = $"{Constants.Text.Loading}({reload.pos}/{reload.max}) {reload.progress * 100:f2}%";
                                LoadingScreen.Instance.SetProgress(msg, reload.progress);
                                yield return null;
                            }
                            UpdateVersion();
                        }
                    }
                }
            }

            LoadingScreen.Instance.SetVisible(false);
            completed?.Invoke();

            // StartCoroutine(CheckVersion());
            yield return CheckVersion();
            
            if (updateVersion <= 0)
            {
                string error = Bootstrap.use_data_dir("", "");
                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show(Constants.Text.Tips, "use failed. empty path error:" + error);
                }
                yield break;
            }

            // StartCoroutine(PreparePatchAndRestart());
            yield return PreparePatchAndRestart();

        }
        
        private IEnumerator CheckVersion()
        {
            var getPatchUpdateInfoAsync = Assets.GetPatchUpdateInfoAsync();
            yield return getPatchUpdateInfoAsync;
            if (getPatchUpdateInfoAsync.result != Request.Result.Success) { yield break; }

            if (Assets.PlayerAssets.patchVersion < getPatchUpdateInfoAsync.info.version)
            {
                updateVersion = getPatchUpdateInfoAsync.info.version;
            }
        }
        
        private IEnumerator PreparePatchAndRestart()
        {
            //1. clear files if exist
            string runtimePatchPath = string.Format(RUNTIME_PATCH_PATH_FORMAT, updateVersion);
            if (Directory.Exists(runtimePatchPath)) { Directory.Delete(runtimePatchPath, true); }
            Directory.CreateDirectory(runtimePatchPath);

            var patchAsync = Assets.PatchAsync(updateVersion);
            yield return patchAsync;
            if (patchAsync.result != Request.Result.Success) { yield break; }
            ZipHelper.UnZipBytes(patchAsync.patchZip, runtimePatchPath, "", true);
            
            //3. prepare libil2cpp, unzip with name: libil2cpp.so.new
            string zipLibil2cppPath = runtimePatchPath + "/lib_" + Bootstrap.get_arch_abi() + "_libil2cpp.so.zip";
            if (!File.Exists(zipLibil2cppPath))
            {
                MessageBox.Show(Constants.Text.Tips, "file not found:" + zipLibil2cppPath);
                yield break;
            }
            ZipHelper.UnZip(zipLibil2cppPath, runtimePatchPath, "", true);

            //4. tell libboostrap.so to use the right patch after reboot
            string apkPath = "";
            string error = Bootstrap.use_data_dir(runtimePatchPath, apkPath);
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(Constants.Text.Tips, "use failed. path:" + zipLibil2cppPath + ", error:" + error);
                yield break;
            }

            //5. clear unity cache
            string cacheDir = Application.persistentDataPath + "/il2cpp";
            if (Directory.Exists(cacheDir)) {
                DeleteDirectory(cacheDir);
            }
            else
            {
                MessageBox.Show(Constants.Text.Tips, "pre Unity cached file not found. path:" + cacheDir);
                yield break;
            }
        }
        
        public static void DeleteDirectory(string target_dir)
        {
            try
            {
                string[] files = Directory.GetFiles(target_dir);
                string[] dirs = Directory.GetDirectories(target_dir);

                foreach (string file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }

                foreach (string dir in dirs)
                {
                    DeleteDirectory(dir);
                }

                Directory.Delete(target_dir, false);
            }
            catch(System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void ClearAsync()
        {
            var dir = Assets.DownloadDataPath;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        private void UpdateVersion()
        {
            if (version == null) return;
            version.text = $"{Constants.Text.Version}{Assets.Versions}";
        }

        private IEnumerator Downloading()
        {
            while (_downloadAsync.result != DownloadRequestBase.Result.Success)
            {
                var downloadedBytes = Utility.FormatBytes(_downloadAsync.downloadedBytes);
                var downloadSize = Utility.FormatBytes(_downloadAsync.downloadSize);
                var bandwidth = Utility.FormatBytes(_downloadAsync.bandwidth);
                var msg = $"{Constants.Text.Loading}{downloadedBytes}/{downloadSize}, {bandwidth}/s";
                LoadingScreen.Instance.SetProgress(msg, _downloadAsync.progress);
                yield return null;
                if (!_downloadAsync.isDone || string.IsNullOrEmpty(_downloadAsync.error)) continue;
                var retry = MessageBox.Show(Constants.Text.Tips, Constants.Text.TipsDownloadFailed);
                yield return retry;
                if (retry.result == Request.Result.Success) _downloadAsync.Retry();
                else break;
            }
        }

        private IEnumerator Clearing()
        {
            var bundles = new HashSet<string>();
            foreach (var item in versions.data)
            {
                bundles.Add(item.file);
                foreach (var bundle in item.manifest.bundles)
                    bundles.Add(bundle.file);
            }

            //TODO：如果下载目录有自定义的数据可以添加到 bundles 里面防止被删除

            var files = new List<string>();
            foreach (var item in Assets.Versions.data)
            {
                if (!bundles.Contains(item.file))
                    files.Add(item.file);
                foreach (var bundle in item.manifest.bundles)
                    if (!bundles.Contains(bundle.file))
                        files.Add(item.file);
            }

            var removeAsync = new RemoveRequest();
            foreach (var file in files)
            {
                var path = Assets.GetDownloadDataPath(file);
                removeAsync.files.Add(path);
            }

            removeAsync.SendRequest();
            while (!removeAsync.isDone)
            {
                var msg = $"清理历史文件 {removeAsync.current}/{removeAsync.max}";
                LoadingScreen.Instance.SetProgress(msg, removeAsync.progress);
                yield return null;
            }
        }
    }
}