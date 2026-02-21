using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class WaitsGroupEntity : WaitEntity
{

    internal WaitsGroupEntity()
    {
        WaitType = WaitType.GroupWaitAll;
    }

    public string GroupMatchFuncName { get; internal set; }

    internal override bool IsCompleted()
    {
        var completed = false;
        switch (WaitType)
        {
            case WaitType.GroupWaitAll:
                completed = ChildWaits?.All(x => x.Status == WaitStatus.Completed) is true;
                break;

            case WaitType.GroupWaitFirst:
                completed = ChildWaits?.Any(x => x.Status == WaitStatus.Completed) is true;
                break;

            case WaitType.GroupWaitWithExpression when GroupMatchFuncName != null:
                var isCompleted = (bool)InvokeCallback(GroupMatchFuncName, ToWaitsGroup());
                Status = isCompleted ? WaitStatus.Completed : Status;
                return isCompleted;

            case WaitType.GroupWaitWithExpression:
                completed = ChildWaits?.Any(x => x.Status == WaitStatus.Waiting) is false;
                break;
        }
        return completed;
    }

    internal override void OnAddWait()
    {
        ActionOnChildrenTree(w => w.IsRoot = w.ParentWait == null && w.ParentWaitId == null);
        base.OnAddWait();
    }
    
    internal override bool ValidateWaitRequest()
    {
        if (ChildWaits == null || !ChildWaits.Any())
        {
            FunctionState.AddLog(
                $"The group wait named [{Name}] does not have childern, You must add one wait at least.",
                LogType.Error,
                StatusCodes.WaitValidation);
        }
        if (ChildWaits.Any(x => x == null))
        {
            FunctionState.AddLog(
                $"The group wait named [{Name}] contains wait that has null value.",
                LogType.Error,
                StatusCodes.WaitValidation);
        }
        return base.ValidateWaitRequest();
    }
    internal WaitsGroup ToWaitsGroup() => new WaitsGroup(this);
}