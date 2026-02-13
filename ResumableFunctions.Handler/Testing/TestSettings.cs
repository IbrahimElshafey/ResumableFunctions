using Hangfire;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Testing
{
    internal class TestSettings : IResumableFunctionsSettings
    {

        private readonly string _testName;



        public TestSettings(string testName)
        {
            _testName = testName;
            CurrentWaitsDbName = _testName;
        }
        public IGlobalConfiguration HangfireConfig => null;

        public DbContextOptionsBuilder WaitsDbConfig =>
         new DbContextOptionsBuilder()
         .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={_testName};Trusted_Connection=True;TrustServerCertificate=True;");


        public string CurrentServiceUrl => null;

        public string[] DllsToScan => null;

        public bool ForceRescan { get; set; } = true;
        public string CurrentWaitsDbName { get; set; }
        public int CurrentServiceId { get; set; } = -1;

        public IDistributedLockProvider DistributedLockProvider => new NoLockProvider();
        public CleanDatabaseSettings CleanDbSettings => new CleanDatabaseSettings();
        public WaitStatus WaitStatusIfProcessingError { get; set; } = WaitStatus.InError;
    }
}