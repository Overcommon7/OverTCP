using System;
using System.Collections.Generic;

namespace OverTCP.File
{
    internal class AsyncPauseToken
    {
        private volatile TaskCompletionSource<bool> mResumeTcs = CreateCompletedTcs();

        private static TaskCompletionSource<bool> CreateCompletedTcs()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(true);
            return tcs;
        }

        public Task WaitIfPausedAsync() => mResumeTcs.Task;

        public void Pause()
        {
            Interlocked.Exchange(ref mResumeTcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        public bool Resume()
        {
            return mResumeTcs.TrySetResult(true);
        }
    }
}
