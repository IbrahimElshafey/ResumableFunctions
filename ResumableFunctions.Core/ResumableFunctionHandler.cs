﻿using System.Diagnostics;
using ResumableFunctions.Core.Data;
using ResumableFunctions.Core.InOuts;
using Microsoft.EntityFrameworkCore;

namespace ResumableFunctions.Core;

internal partial class ResumableFunctionHandler
{
    private readonly FunctionDataContext _context;
    private readonly WaitsRepository _waitsRepository;
    private readonly MethodIdentifierRepository _metodIdsRepo;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;

    internal ResumableFunctionHandler(FunctionDataContext context = null, IBackgroundTaskQueue backgroundTaskQueue = null)
    {
        _context = context ?? new FunctionDataContext();
        _waitsRepository = new WaitsRepository(_context);
        _metodIdsRepo = new MethodIdentifierRepository(_context);
        _backgroundTaskQueue = backgroundTaskQueue;
    }

    private async Task DuplicateIfFirst(Wait currentWait)
    {
        if (currentWait.IsFirst)
            await RegisterFirstWait(currentWait.RequestedByFunction.MethodInfo);
    }

    private async Task<bool> MoveFunctionToRecycleBin(Wait currentWait)
    {
        //throw new NotImplementedException();
        return true;
    }
}