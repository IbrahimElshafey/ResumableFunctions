﻿using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.DataAccess.Abstraction;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class LockStateRepo : ILockStateRepo
{
    private readonly string _scanStateLockName;
    private readonly WaitsDataContext _context;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IBackgroundProcess _backgroundJobClient;
    private readonly IResumableFunctionsSettings _settings;

    public LockStateRepo(
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
        _backgroundJobClient = backgroundJobClient;
    }
    public async Task<bool> NoLocks()
    {
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        return await _context.Locks.AnyAsync() is false;
    }

    public async Task<int> AddLockState(string name)
    {
        var toAdd = new LockState
        {
            Name = name,
            ServiceName = _settings.CurrentServiceName,
            Created = DateTime.UtcNow,
            ServiceId = _settings.CurrentServiceId,
        };
        _context.Locks.Add(toAdd);
        await _context.SaveChangesdDirectly();
        return toAdd.Id;
    }

    public async Task<bool> RemoveLockState(int id)
    {
        if (id == -1) return true;
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        await _context.Locks.Where(x => x.Id == id).ExecuteDeleteAsync();
        return true;
    }

    public async Task ResetServiceLockStates()
    {
        await using var lockScanStat = await _lockProvider.AcquireLockAsync(_scanStateLockName);
        var scanStates = _context.Locks.Where(x => x.ServiceName == _settings.CurrentServiceName);
        //todo: reset old scan jobs for current service
        //var jobsToCancel = await scanStates.Select(x => x.JobId).ToListAsync();
        //jobsToCancel.ForEach(jobId => _backgroundJobClient.Delete(jobId));
        await scanStates.ExecuteDeleteAsync();
    }
}
