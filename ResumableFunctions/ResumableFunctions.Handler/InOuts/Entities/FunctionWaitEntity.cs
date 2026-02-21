using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Helpers;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace ResumableFunctions.Handler.InOuts.Entities;

public sealed class FunctionWaitEntity : WaitEntity
{
    internal FunctionWaitEntity()
    {

    }

    //todo:delete this property
    [NotMapped]
    public WaitEntity FirstWait { get;  internal set; }
    internal IAsyncEnumerator<Wait> Runner { get; set; }

    [NotMapped] public MethodInfo FunctionInfo { get;  internal set; }

    internal override bool IsCompleted() => ChildWaits.Any(x => x.Status == WaitStatus.Waiting) is false;

    internal override void OnAddWait()
    {
        IsRoot = ParentWait == null && ParentWaitId == null;
        base.OnAddWait();
    }
    internal override bool ValidateWaitRequest()
    {
        var hasSubFunctionAttribute = FunctionInfo.GetCustomAttributes<SubResumableFunctionAttribute>().Any();
        if (!hasSubFunctionAttribute)
            FunctionState.AddLog(
                  $"You didn't set attribute [{nameof(SubResumableFunctionAttribute)}] for method [{FunctionInfo.GetFullName()}].",
                  LogType.Error,
                  StatusCodes.WaitValidation);
        return base.ValidateWaitRequest();
    }
}
