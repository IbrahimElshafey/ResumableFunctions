﻿using System.Runtime.CompilerServices;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.DataAccess.Abstraction;

namespace ResumableFunctions.Handler.Helpers
{
    public class BackgroundJobExecutor
    {
        private readonly IDistributedLockProvider _lockProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundJobExecutor> _logger;
        private readonly IResumableFunctionsSettings _settings;
        private readonly IScanStateRepo _scanStateRepo;

        public BackgroundJobExecutor(
            IServiceProvider serviceProvider,
            IDistributedLockProvider lockProvider,
            ILogger<BackgroundJobExecutor> logger,
            IResumableFunctionsSettings settings,
            IScanStateRepo scanStateRepo)
        {
            _serviceProvider = serviceProvider;
            _lockProvider = lockProvider;
            _logger = logger;
            _settings = settings;
            _scanStateRepo = scanStateRepo;
        }
        public async Task Execute(
            string lockName,
            Func<Task> backgroundTask,
            string errorMessage,
            bool isScanTask = false,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            int scanTaskId = -1;
            try
            {
                await using var handle =
                    await _lockProvider.TryAcquireLockAsync(_settings.CurrentWaitsDbName + lockName);
                if (handle is null) return;//if another process work on same task then ignore

                using var scope = _serviceProvider.CreateScope();
                if (isScanTask)
                    scanTaskId = await _scanStateRepo.AddScanState(lockName);
                await backgroundTask();
            }
            catch (Exception ex)
            {
                var codeInfo =
                    $"\nSource File Path: {sourceFilePath}\n" +
                    $"Line Number: {sourceLineNumber}";
                errorMessage = errorMessage == null ?
                    $"Error when execute [{methodName}]\n{codeInfo}" :
                    $"{errorMessage}\n{codeInfo}";
                _logger.LogError(ex, errorMessage);
                throw;
            }
            finally
            {
                await _scanStateRepo.RemoveScanState(scanTaskId);
            }
        }
    }
}