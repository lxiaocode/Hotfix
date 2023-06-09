using UnityEngine.Networking;

namespace xasset
{
    public class PatchRequest : Request
    {
        private UnityWebRequest _request;
        public int version;
        public byte[] patchZip { get; private set; }

        protected override void OnStart()
        {
            if (!Assets.Updatable)
            {
                SetResult(Result.Failed);
                return;
            }

            _request = UnityWebRequest.Get(Assets.PlayerAssets.downloadURL + $"/AllAndroidPatchFiles_Version{version}.zip");
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
                patchZip = _request.downloadHandler.data;
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