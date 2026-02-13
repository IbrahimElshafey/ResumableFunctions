using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class ScanLocksRepo : IScanLocksRepo
{
    private readonly string _scanStateLockName;
    private readonly WaitsDataContext _context;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IResumableFunctionsSettings _settings;

    public ScanLocksRepo(
        WaitsDataContext context,
        IDistributedLockProvider lockProvider,
        IResumableFunctionsSettings settings,
        IBackgroundProcess backgroundJobClient)
    {
        _context = context;
        _lockProvider = lockProvider;
        _settings = settings;
        //should not contain ServiceName
        //_scanStateLockName = $"{_settings.CurrentWaitsDbName}_{_settings.CurrentServiceName}_ScanStateLock";
        _scanStateLockName = $"{_settings.CurrentWaitsDbName}_ScanStateLock";
    }

    public async Task<bool> AreLocksExist()
    {
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        return await _context.ScanLocks.AnyAsync() is false;
    }

    public async Task<int> AddLock(string name)
    {
        var toAdd = new LockState
        {
            Name = name,
            ServiceName = _settings.CurrentServiceName,
            Created = DateTime.UtcNow,
            ServiceId = _settings.CurrentServiceId,
        };
        _context.ScanLocks.Add(toAdd);
        await _context.SaveChangesDirectly();
        return toAdd.Id;
    }

    public async Task<bool> RemoveLock(int id)
    {
        if (id == -1) return true;
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        await _context.ScanLocks.Where(x => x.Id == id).ExecuteDeleteAsync();
        return true;
    }

    public async Task ResetServiceLocks()
    {
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        var scanStates = _context.ScanLocks.Where(x => x.ServiceName == _settings.CurrentServiceName);
        //todo: reset old scan jobs for current service
        //var jobsToCancel = await scanStates.Select(x => x.JobId).ToListAsync();
        //jobsToCancel.ForEach(jobId => _backgroundJobClient.Delete(jobId));
        await scanStates.ExecuteDeleteAsync();
    }
}
