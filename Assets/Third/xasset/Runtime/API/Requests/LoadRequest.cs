namespace xasset
{
    public abstract class LoadRequest : Request, IRecyclable
    {
        protected int refCount { get; private set; }
        public string path { get; set; }

        protected override void OnCompleted()
        {
            Logger.D($"Load {GetType().Name} {path} {result}.");
        }

        public void Release()
        {
            if (refCount == 0)
            {
                Logger.E($"Release {GetType().Name} {path} too many times {refCount}.");
                return;
            }

            refCount--;
            if (refCount > 0) return;

            Recycler.RecycleAsync(this);
        }

        protected abstract void OnDispose();

        public void WaitForCompletion()
        {
            if (isDone) return;

            if (status == Status.Wait) Start();

            OnWaitForCompletion();
        }

        protected virtual void OnWaitForCompletion()
        {
        }

        protected void LoadAsync()
        {
            if (refCount > 0)
            {
                if (isDone) ActionRequest.CallAsync(Complete);
            }
            else
            {
                SendRequest();
                Recycler.CancelRecycle(this);
            }

            refCount++;
        }

        #region IRecyclable

        public void EndRecycle()
        {
            Logger.D($"Unload {GetType().Name} {path}.");
            OnDispose();
        }

        public virtual bool CanRecycle()
        {
            return isDone;
        }

        public virtual void RecycleAsync()
        {
        }

        public virtual bool Recycling()
        {
            return false;
        }

        #endregion
    }
}