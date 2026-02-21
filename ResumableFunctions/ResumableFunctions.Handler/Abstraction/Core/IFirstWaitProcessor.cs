using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IFirstWaitProcessor
    {
        Task<MethodWaitEntity> CloneFirstWait(MethodWaitEntity firstMatchedMethodWait);
        Task RegisterFirstWait(int functionId, string methodUrn);
    }
}