using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using System.ComponentModel;

namespace ResumableFunctions.Handler.Core
{
    internal class CleaningJob : ICleaningJob
    {
        private readonly IDatabaseCleaning _databaseCleaning;
        private readonly IResumableFunctionsSettings _settings;
        private readonly BackgroundJobExecutor _jobExecutor;
        private readonly IBackgroundProcess _backgroundProcess;
        private const string CleanJob = "Clean Waits Database";
        private const string MarkInactiveTemplatesJobName = "Mark inactive wait templates";
        public CleaningJob(
            IDatabaseCleaning databaseCleaning,
            IResumableFunctionsSettings settings,
            BackgroundJobExecutor jobExecutor,
            IBackgroundProcess backgroundProcess)
        {
            _databaseCleaning = databaseCleaning;
            _settings = settings;
            _jobExecutor = jobExecutor;
            _backgroundProcess = backgroundProcess;
        }

        public void ScheduleCleaningJob()
        {
            _backgroundProcess.AddOrUpdateRecurringJob<CleaningJob>(
                CleanJob, t => t.CleanDatabase(), _settings.CleanDbSettings.RunCleaningCron);

            _backgroundProcess.AddOrUpdateRecurringJob<CleaningJob>(
                 MarkInactiveTemplatesJobName, t => t.MarkInactiveTemplates(), _settings.CleanDbSettings.MarkInactiveWaitTemplatesCron);
        }

        [DisplayName(CleanJob)]
        public async Task CleanDatabase()
        {
            await _jobExecutor.ExecuteWithLock(CleanJob,
                async () =>
                {
                    await _databaseCleaning.CleanSoftDeletedRows();
                    await _databaseCleaning.CleanInactiveWaitTemplates();
                    await _databaseCleaning.CleanCompletedFunctionInstances();
                    await _databaseCleaning.CleanOldPushedCalls();
                },
                $"Error when {CleanJob}");
        }

        [DisplayName(MarkInactiveTemplatesJobName)]
        public async Task MarkInactiveTemplates()
        {
            await _jobExecutor.ExecuteWithLock(MarkInactiveTemplatesJobName,
                _databaseCleaning.MarkInactiveWaitTemplates,
                $"Error when {MarkInactiveTemplatesJobName}");
        }
    }
}