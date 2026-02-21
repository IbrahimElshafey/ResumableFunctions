namespace ResumableFunctions.Handler.Abstraction.Abstraction
{
    public interface IDatabaseCleaning
    {
        Task CleanCompletedFunctionInstances();
        Task MarkInactiveWaitTemplates();
        Task CleanInactiveWaitTemplates();
        Task CleanSoftDeletedRows();
        Task CleanOldPushedCalls();

        //todo: Task DeleteInactiveMethodidentifiers();
    }
}
