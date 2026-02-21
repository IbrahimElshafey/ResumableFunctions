using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Linq.Expressions;
using System.Reflection;

namespace ResumableFunctions.Handler.DataAccess;

internal partial class WaitsRepo
{
    /// <summary>
    /// Add fresh from memory wait
    /// </summary>
    public async Task<bool> SaveWait(WaitEntity newWait)
    {
        _logger.LogInformation($"Starting SaveWait for {newWait.Name} requested by {newWait.RequestedByFunction}");

        if (newWait.ValidateWaitRequest() is false)
        {
            var message =
                $"Error when validate the requested wait [{newWait.Name}] " +
                $"that requested by function [{newWait.RequestedByFunction}].";
            _logger.LogError(message);
        }

        switch (newWait)
        {
            case MethodWaitEntity methodWait:
                _logger.LogInformation($"Saving MethodWaitEntity for {newWait.Name}");
                await SaveMethodWait(methodWait);
                break;
            case WaitsGroupEntity manyWaits:
                _logger.LogInformation($"Saving WaitsGroupEntity for {newWait.Name}");
                await SaveWaitsGroup(manyWaits);
                break;
            case FunctionWaitEntity functionWait:
                _logger.LogInformation($"Saving FunctionWaitEntity for {newWait.Name}");
                await SaveFunctionWait(functionWait);
                break;
            case TimeWaitEntity timeWait:
                _logger.LogInformation($"Handling TimeWaitEntity for {newWait.Name}");
                await HandleTimeWaitRequest(timeWait);
                break;
        }

        _logger.LogInformation($"Finished SaveWait for {newWait.Name}");
        return false;
    }

    public async Task<MethodWaitEntity> GetMethodWait(long waitId, params Expression<Func<MethodWaitEntity, object>>[] includes)
    {
        _logger.LogInformation($"Getting MethodWaitEntity with ID {waitId}");
        var query = _context.MethodWaits.AsQueryable();
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query
            .Where(x => x.Status == WaitStatus.Waiting)
            .FirstOrDefaultAsync(x => x.Id == waitId);
    }

    private async Task SaveMethodWait(MethodWaitEntity methodWait)
    {
        _logger.LogInformation($"Saving MethodWaitEntity for {methodWait.Name}");
        var methodId = await _methodIdsRepo.GetId(methodWait);
        var funcId = methodWait.RequestedByFunctionId;
        var expressionsHash = new ExpressionsHashCalculator(methodWait.MatchExpression, methodWait.AfterMatchAction, methodWait.CancelMethodAction).HashValue;
        methodWait.MethodToWaitId = methodId.MethodId;
        methodWait.MethodGroupToWaitId = methodId.GroupId;

        await SetWaitTemplate();
        await AddWait(methodWait);

        async Task SetWaitTemplate()
        {
            WaitTemplate waitTemplate;
            if (methodWait.TemplateId == default)
            {
                waitTemplate =
                    await _waitTemplatesRepo.CheckTemplateExist(expressionsHash, funcId, methodId.GroupId) ??
                    await _waitTemplatesRepo.AddNewTemplate(expressionsHash, methodWait);
            }
            else
            {
                waitTemplate = await _waitTemplatesRepo.GetById(methodWait.TemplateId);
            }
            methodWait.TemplateId = waitTemplate.Id;
            methodWait.Template = waitTemplate;
        }
    }

    private async Task SaveWaitsGroup(WaitsGroupEntity waitsGroup)
    {
        _logger.LogInformation($"Saving WaitsGroupEntity for {waitsGroup.Name}");
        for (var index = 0; index < waitsGroup.ChildWaits.Count; index++)
        {
            var childWait = waitsGroup.ChildWaits[index];
            childWait.FunctionState = waitsGroup.FunctionState;
            childWait.RequestedByFunctionId = waitsGroup.RequestedByFunctionId;
            childWait.RequestedByFunction = waitsGroup.RequestedByFunction;
            childWait.StateAfterWait = waitsGroup.StateAfterWait;
            childWait.ParentWait = waitsGroup;
            childWait.CurrentFunction = waitsGroup.CurrentFunction;
            await SaveWait(childWait);
        }

        await AddWait(waitsGroup);
    }

    private async Task SaveFunctionWait(FunctionWaitEntity functionWait)
    {
        _logger.LogInformation($"Saving FunctionWaitEntity for {functionWait.Name}");
        try
        {
            var functionRunner =
                functionWait.Runner != null ?
                new FunctionRunner(functionWait.Runner) :
                new FunctionRunner(functionWait.CurrentFunction, functionWait.FunctionInfo);
            var hasNext = await functionRunner.MoveNextAsync();
            functionWait.FirstWait = functionRunner.CurrentWaitEntity;
            if (hasNext is false)
            {
                _logger.LogWarning($"No waits exist in sub function ({functionWait.FunctionInfo.GetFullName()})");
                return;
            }

            functionWait.FirstWait = functionRunner.CurrentWaitEntity;
            functionWait.FirstWait.FunctionState = functionWait.FunctionState;
            functionWait.FirstWait.FunctionStateId = functionWait.FunctionState.Id;
            functionWait.FirstWait.ParentWait = functionWait;
            functionWait.FirstWait.ParentWaitId = functionWait.Id;
            functionWait.FirstWait.IsFirst = functionWait.IsFirst;
            functionWait.FirstWait.WasFirst = functionWait.WasFirst;
            var methodId = await _methodIdsRepo.GetResumableFunction(new MethodData(functionWait.FunctionInfo));
            functionWait.FirstWait.RequestedByFunction = methodId;
            functionWait.FirstWait.RequestedByFunctionId = methodId.Id;

            await SaveWait(functionWait.FirstWait);//first wait for sub function
            await AddWait(functionWait);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error when saving function wait for {functionWait.Name}");
            await _logsRepo.AddErrorLog(ex, "When save function wait", StatusCodes.WaitValidation);
        }
    }

    private async Task HandleTimeWaitRequest(TimeWaitEntity timeWait)
    {
        _logger.LogInformation($"Handling TimeWaitEntity for {timeWait.Name}");
        var timeWaitCallbackMethod = timeWait.TimeWaitMethod;

        var methodId = await _methodIdsRepo.GetId(timeWaitCallbackMethod);

        var timeWaitInput = new TimeWaitInput
        {
            TimeMatchId = timeWait.UniqueMatchId,
            RequestedByFunctionId = timeWait.RequestedByFunctionId,
            Description = $"[{timeWait.Name}] in function [{timeWait.RequestedByFunction.RF_MethodUrn}:{timeWait.FunctionState.Id}]"
        };
        if (!timeWait.IgnoreJobCreation)
            timeWaitCallbackMethod.ExtraData.JobId = _backgroundJobClient.Schedule(
                () => new LocalRegisteredMethods().TimeWait(timeWaitInput), timeWait.TimeToWait);

        timeWaitCallbackMethod.MethodToWaitId = methodId.MethodId;
        timeWaitCallbackMethod.MethodGroupToWaitId = methodId.GroupId;

        await SaveMethodWait(timeWaitCallbackMethod);
        timeWaitCallbackMethod.MandatoryPart = timeWait.UniqueMatchId;
        _context.Entry(timeWait).State = EntityState.Detached;
    }

    /// <summary>
    /// Add waits from leefs to roots
    /// </summary>
    public Task AddWait(WaitEntity wait)
    {
        _logger.LogInformation($"Adding WaitEntity for {wait.Name}");
        var isTracked = _context.Waits.Local.Contains(wait);
        var isAddStatus = _context.Entry(wait).State == EntityState.Added;
        wait.OnAddWait();

        if (isTracked || isAddStatus) return Task.CompletedTask;

        _logger.LogInformation($"Add Wait [{wait.Name}] with type [{wait.WaitType}]");
        switch (wait)
        {
            case WaitsGroupEntity waitGroup:
                waitGroup.ChildWaits.RemoveAll(x => x is TimeWaitEntity);
                break;
            case MethodWaitEntity { MethodToWaitId: > 0 } methodWait:
                methodWait.MethodToWait = null;
                break;
        }

        _context.Waits.Add(wait);
        return Task.CompletedTask;
    }
}
