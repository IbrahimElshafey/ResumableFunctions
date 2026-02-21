namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IWaitsProcessor
    {
        Task FindWorkflowMatchedWaits(int workflowId, long pushedCallId, int methodGroupId, DateTime pushedCallDate);
    }
}