using Hangfire;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.InOuts;
using System.Reflection;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IResumableFunctionsSettings
    {
        IGlobalConfiguration HangfireConfig { get; }
        DbContextOptionsBuilder WaitsDbConfig { get; }
        string CurrentServiceUrl { get; }
        int CurrentServiceId { get; set; }

        IDistributedLockProvider DistributedLockProvider { get; }
        string[] DllsToScan { get; }

        bool ForceRescan { get; set; }
        string CurrentWaitsDbName { get; }
        string CurrentServiceName => Assembly.GetEntryAssembly().GetName().Name;

        CleanDatabaseSettings CleanDbSettings { get; }
        WaitStatus WaitStatusIfProcessingError { get; }
    }
}
