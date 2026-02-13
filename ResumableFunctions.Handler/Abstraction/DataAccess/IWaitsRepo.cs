using ResumableFunctions.Handler.DataAccess.InOuts;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Linq.Expressions;
using System.Reflection;

namespace ResumableFunctions.Handler.Abstraction.Abstraction;

public interface IWaitsRepo
{
    //Hint: I didn't use the best practice here , repo should contains query about data and names should not look like a business calls
    Task CancelOpenedWaitsForState(int stateId);
    Task CancelSubWaits(long parentId, long pushedCallId);
    Task<WaitEntity> GetWaitParent(WaitEntity wait);
    Task<List<ImpactedFunctionsIds>> GetImpactedFunctions(string methodUrn, DateTime puhsedCallDate);
    Task<ImpactedFunctionsIds> GetCallEffectionInCurrentService(string methodUrn, DateTime puhsedCallDate);
    Task RemoveFirstWaitIfExist(int methodIdentifierId);
    Task<bool> SaveWait(WaitEntity newWait);
    Task<MethodWaitEntity> GetMethodWait(long waitId, params Expression<Func<MethodWaitEntity, object>>[] includes);
    Task<List<MethodWaitEntity>> GetPendingWaitsForTemplate(
        int templateId,
        string mandatoryPart,
        DateTime pushedCallDate,
        params Expression<Func<MethodWaitEntity, object>>[] includes);
    Task<List<MethodWaitEntity>> GetPendingWaitsForFunction(int rootFunctionId, int methodGroupId,DateTime pushedCallDate);
}