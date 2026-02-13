using Hangfire;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Core
{
    public class InMemoryResumableFunctionsSettings : IResumableFunctionsSettings
    {
        private readonly string _databaseName;

        public InMemoryResumableFunctionsSettings(string databaseName = null)
        {
            _databaseName = databaseName ?? Guid.NewGuid().ToString();
            CurrentWaitsDbName = _databaseName;
        }

        public IGlobalConfiguration HangfireConfig => null;

        public DbContextOptionsBuilder WaitsDbConfig =>
            new DbContextOptionsBuilder().UseInMemoryDatabase(_databaseName);

        public string CurrentServiceUrl { get; set; }
        public string[] DllsToScan { get; set; }
        public bool ForceRescan { get; set; } = true;
        public string CurrentWaitsDbName { get; set; }
        public int CurrentServiceId { get; set; } = -1;

        public IDistributedLockProvider DistributedLockProvider => new NoLockProvider();

        private readonly CleanDatabaseSettings _cleanDbSettings = new CleanDatabaseSettings();
        public CleanDatabaseSettings CleanDbSettings => _cleanDbSettings;

        public WaitStatus WaitStatusIfProcessingError { get; set; } = WaitStatus.InError;

        public InMemoryResumableFunctionsSettings SetDllsToScan(params string[] dlls)
        {
            DllsToScan = dlls;
            return this;
        }

        public InMemoryResumableFunctionsSettings SetCurrentServiceUrl(string serviceUrl)
        {
            CurrentServiceUrl = serviceUrl;
            return this;
        }
    }
}
