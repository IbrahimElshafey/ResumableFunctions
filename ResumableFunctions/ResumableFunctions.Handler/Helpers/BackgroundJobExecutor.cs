using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Core.Abstraction;
using System.Runtime.CompilerServices;

namespace ResumableFunctions.Handler.Helpers
{
    //todo: make this as aspect [BackgroundJob(errorMsg)] [BackgroundJobWithDistributedLock(lockName,errorMsg)]
    //todo: refactor this to be interface that can be injected
    public class BackgroundJobExecutor
    {
        private readonly IDistributedLockProvider _lockProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundJobExecutor> _logger;
        private readonly IResumableFunctionsSettings _settings;

        public BackgroundJobExecutor(
            IServiceProvider serviceProvider,
            IDistributedLockProvider lockProvider,
            ILogger<BackgroundJobExecutor> logger,
            IResumableFunctionsSettings settings)
        {
            _serviceProvider = serviceProvider;
            _lockProvider = lockProvider;
            _logger = logger;
            _settings = settings;
        }

        /// <summary>
        /// Create new scope and execute the `backgroundTask` while lock is created and no instance of `backgroundTask`
        /// can be executed.
        /// </summary>
        /// <returns></returns>
        public async Task ExecuteWithLock(
            string lockName,
            Func<Task> backgroundTask,
            string errorMessage,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                await using var handle =
                    await _lockProvider.TryAcquireLockAsync(_settings.CurrentWaitsDbName + lockName);
                if (handle is null) return;//if another process work on same task then ignore

                using var scope = _serviceProvider.CreateScope();
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
        }


        public async Task ExecuteWithoutLock(
            Func<Task> backgroundTask,
            string errorMessage,
            [CallerMemberName] string methodName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
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
        }

    }
}