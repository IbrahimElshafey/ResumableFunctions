using MessagePack;
using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.DataAccess;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using ResumableFunctions.Handler.UiService.InOuts;
using System.Collections;

namespace ResumableFunctions.Handler.UiService
{
    internal class UiService : IUiService
    {
        private readonly WaitsDataContext _context;

        //todo: UI service must not use EF context directly
        public UiService(WaitsDataContext context)
        {
            _context = context;
        }



        public async Task<List<ServiceInfo>> GetServicesSummary()
        {
            var result = new List<ServiceInfo>();
            var services =
                await _context.ServicesData
                .Where(x => x.ParentId == -1)
                .ToListAsync();

            var serviceErrors =
                await _context.Logs
               .Where(x => x.LogType == LogType.Error)
               .GroupBy(x => x.ServiceId)
               .Select(x => new { ServiceId = x.Key, ErrorsCount = x.Count() })
               .ToDictionaryAsync(x => x.ServiceId);

            var methodsCounts =
                await _context.MethodIdentifiers
                .GroupBy(x => x.ServiceId)
                .Select(x => new
                {
                    FunctionsCount = x.Count(x => x.Type == MethodType.ResumableFunctionEntryPoint),
                    MethodsCount = x.Count(x => x.Type == MethodType.MethodWait),
                    ServiceId = x.Key
                })
                .ToDictionaryAsync(x => x.ServiceId);

            var pushedCalls =
                await _context.PushedCalls
                .GroupBy(x => x.ServiceId)
                .Select(x => new { ServiceId = x.Key, PushedCalls = x.Count() })
                .ToDictionaryAsync(x => x.ServiceId);

            var scanStatus =
                await _context.ScanLocks
                .Select(x => x.ServiceId)
                .Distinct()
                .ToListAsync();

            foreach (var service in services)
            {
                var serviceInfo = new ServiceInfo(service.Id, service.AssemblyName, service.Url, service.ReferencedDlls, service.Created, service.Modified);

                if (serviceErrors.TryGetValue(service.Id, out var error))
                    serviceInfo.LogErrors = error.ErrorsCount;

                if (methodsCounts.TryGetValue(service.Id, out var methodsCounter))
                {
                    serviceInfo.FunctionsCount = methodsCounter.FunctionsCount;
                    serviceInfo.MethodsCount = methodsCounter.MethodsCount;
                }

                if (pushedCalls.TryGetValue(service.Id, out var pushedCallsCount))
                    serviceInfo.PushedCallsCount = pushedCallsCount.PushedCalls;

                if (scanStatus.Contains(service.Id))
                    serviceInfo.IsScanRunning = true;

                result.Add(serviceInfo);
            }
            return result;
        }


        public async Task<List<LogRecord>> GetLogs(int page = 0, int serviceId = -1, int statusCode = -1)
        {
            var query = _context.Logs.AsQueryable();

            if (serviceId != -1)
                query = query.Where(x => x.ServiceId == serviceId);

            if (statusCode != -1)
                query = query.Where(x => x.StatusCode == statusCode);

            if (serviceId == -1 && statusCode == -1)
                query = _context.Logs.Where(x => x.LogType != LogType.Info);

            return await
                query
                .OrderByDescending(x => x.Id)
                //.Skip(page * 100)
                //.Take(100)
                .ToListAsync();
        }

        public async Task<List<FunctionInfo>> GetFunctionsSummary(int serviceId = -1, string searchTerm = null)
        {
            var query = _context.ResumableFunctionIdentifiers.AsNoTracking();

            if (serviceId != -1)
                query = query.Where(x => x.ServiceId == serviceId);

            if (int.TryParse(searchTerm, out var id))
                query = query.Where(x => x.Id == id);
            else if (searchTerm != null)
                query = query.Where(x => x.RF_MethodUrn.Contains(searchTerm));

            return await query
              .Include(x => x.ActiveFunctionsStates)
              .Include(x => x.WaitsCreatedByFunction)
              .Where(x => x.Type == MethodType.ResumableFunctionEntryPoint)
              .Select(x => new FunctionInfo(
                      x,
                      x.WaitsCreatedByFunction.First(x => x.IsFirst && x.IsRoot).Name,
                      x.ActiveFunctionsStates.Count(x => x.Status == FunctionInstanceStatus.InProgress),
                      x.ActiveFunctionsStates.Count(x => x.Status == FunctionInstanceStatus.Completed),
                      x.ActiveFunctionsStates.Count(x => x.Status == FunctionInstanceStatus.InError)
                      ))
              .ToListAsync();
        }

        public async Task<List<MethodGroupInfo>> GetMethodGroupsSummary(int serviceId = -1, string searchTerm = null)
        {
            // int Id, string URN, int MethodsCount,int ActiveWaits,int CompletedWaits,int CanceledWaits
            var waitsQuery = _context
                .MethodWaits
                .GroupBy(x => x.MethodGroupToWaitId)
                .Select(x => new
                {
                    Waiting = (int?)x.Count(x => x.Status == WaitStatus.Waiting),
                    Completed = (int?)x.Count(x => x.Status == WaitStatus.Completed),
                    Canceled = (int?)x.Count(x => x.Status == WaitStatus.Canceled),
                    MethodGroupId = (int?)x.Key
                });

            var methodGroupsQuery = _context
                .MethodsGroups
                .Include(x => x.WaitMethodIdentifiers)
                .Select(x => new
                {
                    MethodsCount = x.WaitMethodIdentifiers.Count,
                    Group = x,
                });

            if (serviceId != -1)
            {
                var methodGroupsToInclude =
                    await _context.WaitMethodIdentifiers
                    .Where(x => x.ServiceId == serviceId)
                    .Select(x => x.MethodGroupId)
                    .Distinct()
                    .ToListAsync();
                methodGroupsQuery =
                    methodGroupsQuery.Where(x => methodGroupsToInclude.Contains(x.Group.Id));
            }

            if(int.TryParse(searchTerm, out var id))
                methodGroupsQuery = methodGroupsQuery.Where(x => x.Group.Id == id);
            else if (searchTerm != null)
                methodGroupsQuery = methodGroupsQuery.Where(x => x.Group.MethodGroupUrn.Contains(searchTerm));

            var join =
                from methodGroup in methodGroupsQuery
                join wait in waitsQuery on methodGroup.Group.Id equals wait.MethodGroupId into jo
                from item in jo.DefaultIfEmpty()
                select new { wait = item, methodGroup };

            return (await join.ToListAsync())
                .Select(x =>
                    new MethodGroupInfo(
                    x.methodGroup.Group,
                    x.methodGroup.MethodsCount,
                    x.wait?.Waiting ?? 0,
                    x.wait?.Completed ?? 0,
                    x.wait?.Canceled ?? 0,
                    x.methodGroup.Group.Created))
                .ToList();
        }

        public async Task<List<PushedCallInfo>> GetPushedCalls(
            int page = 0,
            int serviceId = -1,
            string searchTerm = null)
        {
            var counts =
                _context
                .WaitProcessingRecords
                .GroupBy(x => x.PushedCallId)
                .Select(x => new
                {
                    CallId = (long?)x.Key,
                    All = (int?)x.Count(),
                    Matched = (int?)x.Count(waitForCall => waitForCall.MatchStatus == MatchStatus.Matched),
                    NotMatched = (int?)x.Count(waitForCall =>
                        waitForCall.MatchStatus == MatchStatus.NotMatched),
                });

            var query =
                from call in _context.PushedCalls
                orderby call.Id descending
                join counter in counts on call.Id equals counter.CallId into joinResult
                from item in joinResult.DefaultIfEmpty()
                select new { item, call };

            //query = query.Skip(page * 100).Take(100);

            if (serviceId > -1)
                query = query.Where(x => x.call.ServiceId == serviceId);
            if (searchTerm != null)
                query = query.Where(x => x.call.MethodUrn.Contains(searchTerm));

            var result = (await query.ToListAsync())
                .Select(x => new PushedCallInfo(
                    x.call,
                    x.item?.All ?? 0,
                    x.item?.Matched ?? 0,
                    x.item?.NotMatched ?? 0
                )).ToList();

            result.ForEach(x => x.PushedCall.LoadUnmappedProps());
            return result;
        }

        public async Task<List<FunctionInstanceInfo>> GetFunctionInstances(int functionId)
        {
            var query =
                 _context.FunctionStates
                 .Where(x => x.ResumableFunctionIdentifierId == functionId)
                 .Include(x => x.Waits)
                 .Select(functionState => new FunctionInstanceInfo(
                     functionState,
                     functionState.Waits.First(wait => wait.IsRoot && wait.Status == WaitStatus.Waiting),
                     functionState.Waits.Count,
                     functionState.Id
                     ));
            var result = await query.ToListAsync();
            //result.ForEach(x => x.FunctionState.LoadUnmappedProps());
            return result;
        }

        public async Task<PushedCallDetails> GetPushedCallDetails(long pushedCallId)
        {
            var pushedCall = await _context.PushedCalls.FindAsync(pushedCallId);
            pushedCall.LoadUnmappedProps();
            var methodData = pushedCall.MethodData;
            var inputOutput = MessagePackSerializer.ConvertToJson(pushedCall.DataValue);
            var callExpectedMatches =
                await _context
                .WaitProcessingRecords
                .Where(x => x.PushedCallId == pushedCallId)
                .ToListAsync();
            var waitsIds = callExpectedMatches.Select(x => x.WaitId).ToList();

            var waits =
                await (
                from wait in _context.MethodWaits.Include(x => x.RequestedByFunction).Where(x => waitsIds.Contains(x.Id))
                from template in _context.WaitTemplates
                where wait.TemplateId == template.Id
                select new
                {
                    wait.Id,
                    wait.Name,
                    wait.Status,
                    wait.RequestedByFunction.RF_MethodUrn,
                    wait.RequestedByFunctionId,
                    wait.FunctionStateId,
                    template.MatchExpressionValue,
                    template.AfterMatchAction,
                    template.InstanceMandatoryPartExpressionValue,
                    wait.MandatoryPart
                })
                .ToListAsync();

            var waitsForCall =
                (from callMatch in callExpectedMatches
                 from wait in waits
                 where callMatch.WaitId == wait.Id
                 select new MethodWaitDetails(
                    wait.Name,
                    wait.Id,
                    wait.Status,
                    wait.RequestedByFunctionId,
                    wait.RF_MethodUrn,
                    wait.FunctionStateId,
                    callMatch.Created,
                    wait.MandatoryPart,
                    callMatch.MatchStatus,
                    callMatch.AfterMatchActionStatus,
                    callMatch.ExecutionStatus,
                    new TemplateDisplay(
                        wait.MatchExpressionValue,
                        wait.InstanceMandatoryPartExpressionValue)
                    ))
                .ToList();
            return new PushedCallDetails(inputOutput, methodData, waitsForCall);
        }

        public async Task<FunctionInstanceDetails> GetFunctionInstanceDetails(int instanceId)
        {
            var instance =
                await _context
                .FunctionStates
                .Include(x => x.ResumableFunctionIdentifier)
                .FirstAsync(x => x.Id == instanceId);

            var logs =
                await _context
                .Logs
                .Where(x => x.EntityId == instanceId && x.EntityType == EntityType.FunctionInstanceLog)
                .ToListAsync();

            var waits =
                await _context.Waits
                .Where(x => x.FunctionStateId == instanceId)
                .ToListAsync();
            await LoadMethodWaitDetails(waits);
            var waitsNodes = new ArrayList(waits.Where(x => x.ParentWait == null).ToList());
            return new FunctionInstanceDetails(
                instanceId,
                instance.ResumableFunctionIdentifier.Id,
                instance.ResumableFunctionIdentifier.RF_MethodUrn,
                $"{instance.ResumableFunctionIdentifier.ClassName}.{instance.ResumableFunctionIdentifier.MethodName}",
                instance.Status,
                MessagePackSerializer.ConvertToJson(instance.StateObjectValue),
                instance.Created,
                instance.Modified,
                logs.Count(x => x.LogType == LogType.Error),
                waitsNodes,
                logs
                );
        }

        public async Task<List<MethodInGroupInfo>> GetMethodsInGroup(int groupId)
        {
            var groupUrn =
                await _context
                .MethodsGroups
                .Where(x => x.Id == groupId)
                .Select(x => x.MethodGroupUrn)
                .FirstOrDefaultAsync();
            var query =
                from method in _context.WaitMethodIdentifiers
                from service in _context.ServicesData
                where method.ServiceId == service.Id && method.MethodGroupId == groupId
                select new { method, ServiceName = service.AssemblyName };
            return
                (await query.ToListAsync())
                .Select(x => new MethodInGroupInfo(
                    x.ServiceName,
                    x.method,
                    groupUrn))
                .ToList();
        }

        public async Task<List<MethodWaitDetails>> GetWaitsInGroup(int groupId)
        {
            var groupName =
                await _context
                    .MethodsGroups
                    .Where(x => x.Id == groupId)
                    .Select(x => x.MethodGroupUrn)
                    .FirstOrDefaultAsync();
            var query =
                from methodWait in _context.MethodWaits.Include(x => x.RequestedByFunction)
                from template in _context.WaitTemplates
                where methodWait.TemplateId == template.Id && methodWait.MethodGroupToWaitId == groupId
                select new
                {
                    methodWait,
                    template.MatchExpressionValue,
                    template.AfterMatchAction,
                    template.CallMandatoryPartPaths,
                    methodWait.RequestedByFunction.RF_MethodUrn
                };

            var result =
                (await query.ToListAsync())
                .Select(x =>
                    new MethodWaitDetails(
                        x.methodWait.Name,
                        x.methodWait.Id,
                        x.methodWait.Status,
                        x.methodWait.RequestedByFunctionId,
                        x.RF_MethodUrn,
                        x.methodWait.FunctionStateId,
                        x.methodWait.Created,
                        x.methodWait.MandatoryPart,
                        MatchStatus.ExpectedMatch,
                        ExecutionStatus.NotStartedYet,
                        ExecutionStatus.NotStartedYet,
                        new TemplateDisplay(x.MatchExpressionValue, x.CallMandatoryPartPaths)
                    )
                    {
                        CallId = x.methodWait.CallId,
                        GroupName = groupName
                    })
                .ToList();

            return result;
        }

        private async Task LoadMethodWaitDetails(List<WaitEntity> waits)
        {
            foreach (var wait in waits)
            {
                if (wait is MethodWaitEntity mw)
                {
                    mw = await _context.MethodWaits.
                        Include(x => x.ClosureData).
                        Include(x => x.Locals).
                        FirstAsync(x => x.Id == mw.Id);
                    mw.Template = await _context.WaitTemplates
                        .Select(template => new WaitTemplate
                        {
                            MatchExpressionValue = template.MatchExpressionValue,
                            AfterMatchAction = template.AfterMatchAction,
                            CallMandatoryPartPaths = template.CallMandatoryPartPaths,
                            Id = template.Id
                        }).FirstAsync(x => x.Id == mw.TemplateId);
                }
            }
        }

        public async Task<List<ServiceData>> GetServices()
        {
            return
                await _context.ServicesData
                .Select(x => new ServiceData { Id = x.Id, AssemblyName = x.AssemblyName, ParentId = x.ParentId })
                .Where(x => x.ParentId == -1)
                .ToListAsync();
        }
    }
}
