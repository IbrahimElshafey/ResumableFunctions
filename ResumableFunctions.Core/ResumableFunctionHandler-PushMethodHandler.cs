﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using ResumableFunctions.Core.Attributes;
using ResumableFunctions.Core.InOuts;
using Microsoft.EntityFrameworkCore;

namespace ResumableFunctions.Core;

internal partial class ResumableFunctionHandler
{
    /// <summary>
    ///     When method called and finished
    /// </summary>
    internal async Task MethodCalled(PushedMethod pushedMethod)
    {
        await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(WaitProcessing);
    }

    private async ValueTask WaitProcessing(PushedMethod pushedMethod, CancellationToken arg)
    {
        //todo:move this code to background task
        /*
         * Notify cuurent service HandleWaitsBackground
         * Save `pushedMethod` to database table
         * GetMethodActiveWaits
         * Save active waits to ActiveWaits table (wait id, pushed method id, AssemblyName, status)
         * Handle this list one by one
         * If wait owned by current service,Handle local 
         * If external notify the other service with ID
         */
        try
        {
            var matchedWaits = await _waitsRepository.GetMethodActiveWaits(pushedMethod.MethodData);
            foreach (var methodWait in matchedWaits)
            {
                var isLocalWait =
                    methodWait.RequestedByFunction.AssemblyName ==
                    Assembly.GetEntryAssembly().GetName().Name;//Todo:get from "ServiceName" in config
                if (isLocalWait)
                {
                    //handle if local
                    if (!await CheckIfMatch(pushedMethod, methodWait))
                        continue;
                    methodWait.UpdateFunctionData();
                    await ResumeExecution(methodWait);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    //notify sevrice that owns the wait
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Write(ex);
            WriteMessage(ex.Message);
        }
    }

    private async Task<bool> CheckIfMatch(PushedMethod pushedMethod, MethodWait methodWait)
    {
        methodWait.LoadExpressions();
        methodWait.Input = pushedMethod.Input;
        methodWait.Output = pushedMethod.Output;
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

        return false;

        async Task LoadWaitFunctionState(MethodWait wait)
        {
            wait.FunctionState = await _context.FunctionStates.FindAsync(wait.FunctionStateId);
        }
    }

}