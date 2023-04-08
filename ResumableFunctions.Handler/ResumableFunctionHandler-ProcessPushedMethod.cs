﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.InOuts;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ResumableFunctions.Handler;

public partial class ResumableFunctionHandler
{
    /// <summary>
    ///     When method called and finished
    /// </summary>




    private async Task<bool> CheckIfMatch(MethodWait methodWait)
    {
        methodWait.LoadExpressions();
        switch (methodWait.NeedFunctionStateForMatch)
        {
            case false when methodWait.IsMatched():
                await LoadWaitFunctionState(methodWait);
                return true;

            case true:
                await LoadWaitFunctionState(methodWait);
                if (methodWait.IsMatched())
                    return true;
                break;
        }
        await IncrementCompletedCounter(methodWait.PushedMethodCallId);
        return false;

        async Task LoadWaitFunctionState(MethodWait wait)
        {
            wait.FunctionState = await _context.FunctionStates.FindAsync(wait.FunctionStateId);
        }
    }



    private async Task ProcessWait(MethodWait methodWait)
    {
        try
        {
            methodWait.SetInputAndOutput();
            if (!await CheckIfMatch(methodWait))
                return;
            //todo:cancel processing and rewait it if data is locked
            if (methodWait.UpdateFunctionData())
            {
                await ResumeExecution(methodWait);
                await IncrementCompletedCounter(methodWait.PushedMethodCallId);
            }
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error when process matched wait for method [{methodWait.Name}] with id[{methodWait.Id}]");
        }

    }

    private async Task IncrementCompletedCounter(int pushedMethodCallId)
    {
        var entity = await _context.PushedMethodsCalls.FindAsync(pushedMethodCallId);
        entity.CompletedWaitsCount++;
        //if (entity.CompletedWaitsCount == entity.MatchedWaitsCount)
        //    _context.PushedMethodsCalls.Remove(entity);
    }
}