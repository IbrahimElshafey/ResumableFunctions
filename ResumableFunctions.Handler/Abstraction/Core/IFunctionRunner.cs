using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IFunctionRunner
    {
        Wait Current { get; }
        WaitEntity CurrentWaitEntity { get; }
        int State { get; set; }
        ValueTask DisposeAsync();
        ValueTask<bool> MoveNextAsync();
    }
}