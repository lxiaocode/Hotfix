using UnityEngine;
using UnityEngine.Networking;

namespace xasset
{
    public sealed class GetPatchUpdateInfoRequest : Request
    {
        private UnityWebRequest _request;
        public PatchUpdateInfo info { get; private set; }

        protected override void OnStart()
        {
            if (!Assets.Updatable)
            {
                SetResult(Result.Failed);
                return;
            }

            _request = UnityWebRequest.Get(Assets.PlayerAssets.patchUpdateInfoURL);
            _request.certificateHandler = new DownloadCertificateHandler();
            _request.SendWebRequest();
        }

        protected override void OnUpdated()
        {
            progress = _request.downloadProgress;
            if (!_request.isDone)
                return;

            if (string.IsNullOrEmpty(_request.error))
            {
                info = Utility.LoadFromJson<PatchUpdateInfo>(_request.downloadHandler.text);
                
                // 版本文件未发生更新
                if (info.version == Assets.PlayerAssets.patchVersion)
                {
                    SetResult(Result.Failed, "Nothing to update.");
                    return;
                }

                SetResult(Result.Success);
                return;
            }

            SetResult(Result.Failed, _request.error);
        }

        protected override void OnCompleted()
        {
        }
    }
}