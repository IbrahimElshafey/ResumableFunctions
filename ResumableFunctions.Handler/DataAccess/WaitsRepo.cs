using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.DataAccess.InOuts;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Linq.Expressions;

namespace ResumableFunctions.Handler.DataAccess;
internal partial class WaitsRepo : IWaitsRepo
{
    private readonly ILogger<WaitsRepo> _logger;
    private readonly WaitsDataContext _context;
    private readonly IBackgroundProcess _backgroundJobClient;
    private readonly IMethodIdsRepo _methodIdsRepo;
    private readonly IResumableFunctionsSettings _settings;
    private readonly IWaitTemplatesRepo _waitTemplatesRepo;
    private readonly ILogsRepo _logsRepo;

    public WaitsRepo(
        ILogger<WaitsRepo> logger,
        IBackgroundProcess backgroundJobClient,
        WaitsDataContext context,
        IMethodIdsRepo methodIdentifierRepo,
        IResumableFunctionsSettings settings,
        IWaitTemplatesRepo waitTemplatesRepo,
        ILogsRepo logsRepo)
    {
        _logger = logger;
        _context = context;
        _backgroundJobClient = backgroundJobClient;
        _methodIdsRepo = methodIdentifierRepo;
        _settings = settings;
        _waitTemplatesRepo = waitTemplatesRepo;
        _logsRepo = logsRepo;
    }

    public async Task<ImpactedFunctionsIds> GetCallEffectionInCurrentService(string methodUrn, DateTime puhsedCallDate)
    {
        var methodGroup = await GetMethodGroup(methodUrn);
        var affectedFunctions =
            await
            _context.MethodWaits
            .Where(x =>
                x.Status == WaitStatus.Waiting &&
                x.MethodGroupToWaitId == methodGroup.Id &&
                x.ServiceId == _settings.CurrentServiceId &&
                x.Created < puhsedCallDate)
            .Select(x => x.RequestedByFunctionId)
            .Distinct()
            .ToListAsync();
        return affectedFunctions.Any() ?
            new ImpactedFunctionsIds
            {
                AffectedServiceId = _settings.CurrentServiceId,
                AffectedServiceUrl = string.Empty,
                AffectedServiceName = _settings.CurrentServiceName,
                MethodGroupId = methodGroup.Id,
                AffectedFunctionsIds = affectedFunctions,
            }
            : null;
    }

    public async Task<List<ImpactedFunctionsIds>> GetImpactedFunctions(string methodUrn, DateTime puhsedCallDate)
    {
        var methodGroup = await GetMethodGroup(methodUrn);

        var methodWaitsQuery = _context
                   .MethodWaits
                   .Where(x =>
                       x.Status == WaitStatus.Waiting &&
                       x.MethodGroupToWaitId == methodGroup.Id &&
                       x.Created < puhsedCallDate);

        if (methodGroup.IsLocalOnly)
        {
            methodWaitsQuery = methodWaitsQuery.Where(x => x.ServiceId == _settings.CurrentServiceId);
        }

        var affectedFunctionsGroupedByService =
            await methodWaitsQuery
           .Select(x => new { x.RequestedByFunctionId, x.ServiceId })
           .Distinct()
           .GroupBy(x => x.ServiceId)
           .ToListAsync();

        return (
              from service in await _context.ServicesData.Where(x => x.ParentId == -1).ToListAsync()
              from affectedFunction in affectedFunctionsGroupedByService
              where service.Id == affectedFunction.Key
              select new ImpactedFunctionsIds
              {
                  AffectedServiceId = service.Id,
                  AffectedServiceUrl = service.Url,
                  AffectedServiceName = service.AssemblyName,
                  MethodGroupId = methodGroup.Id,
                  AffectedFunctionsIds = affectedFunction.Select(x => x.RequestedByFunctionId).ToList(),
              }
              )
              .ToList();
    }


    private async Task<MethodsGroup> GetMethodGroup(string methodUrn)
    {
        var methodGroup =
           await _context
               .MethodsGroups
               .AsNoTracking()
               .Where(x => x.MethodGroupUrn == methodUrn)
               .FirstOrDefaultAsync();
        if (methodGroup != default)
            return methodGroup;
        var error = $"Method [{methodUrn}] is not registered in current database as [WaitMethod].";
        _logger.LogWarning(error);
        throw new Exception(error);
    }

    public async Task RemoveFirstWaitIfExist(int methodIdentifierId)
    {
        try
        {
            var firstWaitItems =
                 await _context.Waits
                .Where(x =>
                    x.IsFirst &&
                    x.RequestedByFunctionId == methodIdentifierId)
                .ToListAsync();

            if (firstWaitItems != null)
            {
                foreach (var wait in firstWaitItems)
                {
                    wait.IsDeleted = true;
                    if (wait is MethodWaitEntity { MethodWaitType: MethodWaitType.TimeWaitMethod })
                    {
                        wait.LoadUnmappedProps();
                        var jobId = wait.ExtraData["JobId"];
                        _backgroundJobClient.Delete(jobId);
                    }
                }
                //todo:[update] load entity to delete it , concurrency control token and FKs
                if (firstWaitItems.FirstOrDefault()?.FunctionStateId is int stateId)
                {
                    var functionState = await _context
                     .FunctionStates
                     .FirstAsync(x => x.Id == stateId);
                    _context.FunctionStates.Remove(functionState);
                }
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error when RemoveFirstWaitIfExist for function [{methodIdentifierId}]");
        }
    }


    public async Task CancelSubWaits(long parentId, long pushedCallId)
    {
        await CancelChildWaits(parentId);

        async Task CancelChildWaits(long pId)
        {
            var waits = await _context
                .Waits
                .Include(x => x.ClosureData)
                .Where(x => x.ParentWaitId == pId && x.Status == WaitStatus.Waiting)
                .ToListAsync();

            foreach (var wait in waits)
            {
                CancelWait(wait, pushedCallId);//CancelSubWaits
                if (wait.CanBeParent)
                    await CancelChildWaits(wait.Id);
            }
        }
    }

    private void CancelWait(WaitEntity wait, long pushedCallId)
    {
        if (wait.ParentWait != null)//todo:traverse up to get current function
            wait.CurrentFunction = wait.ParentWait.CurrentFunction;
        wait.LoadUnmappedProps();
        wait.Cancel();
        wait.CallId = pushedCallId;

        bool isTimeWait = wait is MethodWaitEntity mw && mw.MethodWaitType == MethodWaitType.TimeWaitMethod;
        if (isTimeWait)
        {
            var jobId = wait.ExtraData["JobId"];
            _backgroundJobClient.Delete(jobId);
        }
    }

    public async Task<WaitEntity> GetWaitParent(WaitEntity wait)
    {
        if (wait?.ParentWaitId != null)
        {
            return await _context
                .Waits
                .Include(x => x.ChildWaits)
                .Include(x => x.RequestedByFunction)
                .FirstOrDefaultAsync(x => x.Id == wait.ParentWaitId);
        }
        return null;
    }


    public async Task CancelOpenedWaitsForState(int stateId)
    {
        await _context.Waits
              .Where(x => x.FunctionStateId == stateId && x.Status == WaitStatus.Waiting)
              .ExecuteUpdateAsync(x => x.SetProperty(wait => wait.Status, _ => WaitStatus.Canceled));
    }

    public async Task<List<MethodWaitEntity>> GetPendingWaitsForTemplate(
        int templateId,
        string mandatoryPart,
        DateTime pushedCallDate,
        params Expression<Func<MethodWaitEntity, object>>[] includes)
    {
        var query = _context
            .MethodWaits
            .Where(
                wait =>
                wait.Status == WaitStatus.Waiting &&
                wait.TemplateId == templateId &&
                wait.Created < pushedCallDate);
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        if (mandatoryPart != null)
        {
            query = query.Where(wait => wait.MandatoryPart == mandatoryPart);
        }
        return
            await query
            .OrderBy(x => x.IsFirst)
            .ToListAsync();
    }

    public async Task<List<MethodWaitEntity>> GetPendingWaitsForFunction(
        int rootFunctionId,
        int methodGroupId,
        DateTime pushedCallDate)
    {
        //todo: use this and delete `GetPendingWaitsForTemplate` and `_templatesRepo.GetWaitTemplatesForFunction`
        // load wait and `template.CallMandatoryPartExpression`
        var waits = await _context
          .MethodWaits
          .Where(
              wait =>
              wait.Status == WaitStatus.Waiting &&
              wait.TemplateId == rootFunctionId &&
              wait.Created < pushedCallDate)
          .ToListAsync();
        var templateIds = waits.Select(x => x.TemplateId);
        var templates = await _context.WaitTemplates.
            Where(x => templateIds.Contains(x.Id)).
            ToDictionaryAsync(x => x.Id, x => x);
        waits.ForEach(x => x.Template = templates[x.TemplateId]);
        return waits;
    }
}