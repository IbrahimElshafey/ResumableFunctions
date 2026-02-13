namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IWaitsProcessor
    {
        Task FindFunctionMatchedWaits(int functionId, long pushedCallId, int methodGroupId, DateTime pushedCallDate);
    }
}