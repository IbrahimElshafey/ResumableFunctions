using Medallion.Threading;

namespace ResumableFunctions.Handler.Core
{
    public class NoLockProvider : IDistributedLockProvider
    {
        public IDistributedLock CreateLock(string name) => new NoLock();

        private class NoLock : IDistributedLock
        {
            public string Name => string.Empty;

            public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                return new NoLockHandle();
            }

            public ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                return new ValueTask<IDistributedSynchronizationHandle>(new NoLockHandle());
            }

            public IDistributedSynchronizationHandle TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default)
            {
                return new NoLockHandle();
            }

            public ValueTask<IDistributedSynchronizationHandle> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
            {
                return new ValueTask<IDistributedSynchronizationHandle>(new NoLockHandle());
            }

            private class NoLockHandle : IDistributedSynchronizationHandle
            {
                public CancellationToken HandleLostToken => CancellationToken.None;

                public void Dispose() { }

                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }
        }
    }
}
