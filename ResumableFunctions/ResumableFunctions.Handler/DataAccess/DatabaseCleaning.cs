using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.DataAccess
{
    internal class DatabaseCleaning : IDatabaseCleaning
    {
        private readonly WaitsDataContext _context;
        private readonly ILogsRepo _logsRepo;
        private readonly IResumableFunctionsSettings _setting;

        public DatabaseCleaning(
            WaitsDataContext context,
            ILogsRepo logsRepo,
            IResumableFunctionsSettings setting)
        {
            _context = context;
            _logsRepo = logsRepo;
            _setting = setting;
        }

        public async Task CleanCompletedFunctionInstances()
        {
            await AddLog("Start to delete compeleted functions instances.");
            var dateThreshold = DateTime.UtcNow.Subtract(_setting.CleanDbSettings.CompletedInstanceRetention);

            var instanceIds =
                await _context.FunctionStates
                .Where(instance => instance.Status == FunctionInstanceStatus.Completed && instance.Modified < dateThreshold)
                .Select(x => x.Id)
                .ToListAsync();
            if (instanceIds.Any())
            {
                using var transaction = _context.Database.BeginTransaction();
                var waitsCount = await _context.Waits
                  .Where(wait => instanceIds.Contains(wait.FunctionStateId))
                  .ExecuteDeleteAsync();

                var privateDataCount = await _context.PrivateData
                  .Where(privateData => instanceIds.Contains(privateData.FunctionStateId.Value))
                  .ExecuteDeleteAsync();

                var instancesCount = await _context.FunctionStates
                    .Where(functionState => instanceIds.Contains(functionState.Id))
                    .ExecuteDeleteAsync();

                var logsCount = await _context.Logs
                    .Where(logItem => 
                            instanceIds.Contains((int)logItem.EntityId) && logItem.EntityType == EntityType.FunctionInstanceLog)
                    .ExecuteDeleteAsync();

                var waitProcessingCount = await _context.WaitProcessingRecords
                    .Where(waitProcessingRecord => instanceIds.Contains(waitProcessingRecord.StateId))
                    .ExecuteDeleteAsync();
                transaction.Commit();
                
                await _logsRepo.AddLog(
                    $"* Delete [{privateDataCount}] private data record.\n"+
                    $"* Delete [{logsCount}] logs related to completed functions instances done.\n"+
                    $"* Delete [{instancesCount}] compeleted functions instances done.\n"+
                    $"* Delete [{waitsCount}] waits related to completed functions instances done.\n"+
                    $"* Delete [{waitProcessingCount}] wait processing record related to completed functions instances done.",
                    LogType.Info,
                    StatusCodes.DataCleaning);
            }
            await AddLog("Delete compeleted functions instances completed.");
        }

        public async Task CleanOldPushedCalls()
        {
            await AddLog("Start to delete old pushed calls.");
            var dateThreshold = DateTime.UtcNow.Subtract(_setting.CleanDbSettings.PushedCallRetention);
            var count =
                await _context.PushedCalls
                .Where(instance => instance.Created < dateThreshold)
                .ExecuteDeleteAsync();
            await AddLog($"Delete [{count}] old pushed calls.");
        }

        public async Task CleanSoftDeletedRows()
        {
            await AddLog("Start to delete soft deleted rows.");

            var count = await _context.Waits
             .Where(instance => instance.IsDeleted)
             .IgnoreQueryFilters()
             .ExecuteDeleteAsync();
            await AddLog($"Delete [{count}] soft deleted waits done.");

            count = await _context.FunctionStates
            .Where(instance => instance.IsDeleted)
            .IgnoreQueryFilters()
            .ExecuteDeleteAsync();
            await AddLog($"Delete [{count}] soft deleted function state done.");
        }

        public async Task MarkInactiveWaitTemplates()
        {
            await AddLog("Start to deactivate unused wait templates.");
            var activeWaitTemplate =
                _context.MethodWaits
                .Where(x => x.Status == WaitStatus.Waiting)
                .Select(x => x.TemplateId)
                .Distinct();
            var count = await _context.WaitTemplates
                .Where(waitTemplate => waitTemplate.IsActive == 1 && !activeWaitTemplate.Contains(waitTemplate.Id))
                .ExecuteUpdateAsync(template => template
                    .SetProperty(x => x.IsActive, -1)
                    .SetProperty(x => x.DeactivationDate, DateTime.UtcNow));
            await AddLog($"Deactivate [{count}] unused wait templates done.");
        }

        public async Task CleanInactiveWaitTemplates()
        {
            await AddLog("Start to delete deactivated wait templates.");
            var dateThreshold = DateTime.UtcNow.Subtract(_setting.CleanDbSettings.DeactivatedWaitTemplateRetention);
            var count = await _context.WaitTemplates
                .Where(template => template.IsActive == -1 && template.DeactivationDate < dateThreshold)
                .ExecuteDeleteAsync();
            await AddLog($"Delete [{count}] deactivated wait templates done.");
        }

        private async Task AddLog(string message)
        {
            await _logsRepo.AddLog(message, LogType.Info, StatusCodes.DataCleaning);
        }
    }
}
