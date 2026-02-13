using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Abstraction.Abstraction;

public interface IPushedCallsRepo
{
    Task<PushedCall> GetById(long pushedCallId);
    Task Push(PushedCall pushedCall);
    Task<bool> PushedCallMatchedForFunctionBefore(long pushedCallId, int rootFunctionId);
}