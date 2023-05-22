﻿using ResumableFunctions.Handler.InOuts;
using System.Linq.Expressions;

namespace ResumableFunctions.Handler.DataAccess.Abstraction
{
    public interface IWaitsRepository
    {
        Task AddWait(Wait wait);
        Task CancelFunctionWaits(int requestedByFunctionId, int functionStateId);
        Task CancelOpenedWaitsForState(int stateId);
        Task CancelSubWaits(int parentId);
        Task<Wait> GetOldWaitForReplay(ReplayRequest replayWait);
        Task<Wait> GetWaitGroup(int? parentGroupId);
        Task<Wait> GetWaitParent(Wait wait);
        Task<List<WaitId>> GetWaitsIdsForMethodCall(int pushedCallId);
        Task RemoveFirstWaitIfExist(int methodIdentifierId);

        Task<Wait> LoadWaitTree(Expression<Func<Wait,bool>> expression);

        Task<bool> SaveWaitRequestToDb(Wait newWait);
    }
}