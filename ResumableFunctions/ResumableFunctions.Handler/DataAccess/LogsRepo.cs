using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class LogsRepo : ILogsRepo
{
    private readonly WaitsDataContext _context;
    private readonly IResumableFunctionsSettings _settings;
    private readonly ILogger<ServiceRepo> _logger;

    public LogsRepo(
        WaitsDataContext context,
        IResumableFunctionsSettings settings,
        ILogger<ServiceRepo> logger)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
    }
    public async Task AddErrorLog(Exception ex, string errorMsg, int statusCode)
    {
        _logger.LogError(ex, errorMsg);
        _context.Logs.Add(new LogRecord
        {
            EntityId = _settings.CurrentServiceId,
            EntityType = EntityType.ServiceLog,
            Message = $"{errorMsg}\n{ex}",
            Created = DateTime.UtcNow,
            ServiceId = _settings.CurrentServiceId,
            LogType = LogType.Error,
            StatusCode = statusCode
        });
        await _context.SaveChangesDirectly();
    }

    public async Task AddLog(string msg, LogType logType, int statusCode)
    {
        _context.Logs.Add(new LogRecord
        {
            EntityId = _settings.CurrentServiceId,
            EntityType = EntityType.ServiceLog,
            Message = msg,
            ServiceId = _settings.CurrentServiceId,
            LogType = logType,
            Created = DateTime.UtcNow,
            StatusCode = statusCode
        });
        await _context.SaveChangesDirectly();
    }

    public async Task AddLogs(LogType logType, int statusCode, params string[] msgs)
    {
        foreach (var msg in msgs)
        {
            _context.Logs.Add(new LogRecord
            {
                EntityId = _settings.CurrentServiceId,
                EntityType = EntityType.ServiceLog,
                Message = msg,
                LogType = logType,
                ServiceId = _settings.CurrentServiceId,
                StatusCode = statusCode,
                Created = DateTime.UtcNow,
            });
        }
        await _context.SaveChangesDirectly();
    }

    public async Task ClearErrorsForFunctionInstance(int functionStateId)
    {
        await _context.Logs.
            Where(x =>
            x.EntityId == functionStateId &&
            x.EntityType == EntityType.FunctionInstanceLog &&
            x.LogType == LogType.Error).
            ExecuteUpdateAsync(row => row.SetProperty(x => x.LogType, LogType.WasError));
    }
}
