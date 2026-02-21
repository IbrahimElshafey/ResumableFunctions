using FastExpressionCompiler;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.Helpers;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class MethodWaitEntity : WaitEntity
{
    internal MethodWaitEntity()
    {

    }

    [NotMapped]
    public string AfterMatchAction { get; protected set; }

    [NotMapped]
    public LambdaExpression MatchExpression { get; protected set; }

    [NotMapped]
    public string CancelMethodAction { get; protected set; }
    public MethodWaitType MethodWaitType { get; internal set; } = MethodWaitType.NormalMethod;
    public string MandatoryPart { get; internal set; }

    [NotMapped]
    internal WaitTemplate Template { get; set; }
    public int TemplateId { get; internal set; }

    [NotMapped]
    internal WaitMethodIdentifier MethodToWait { get; set; }

    internal int? MethodToWaitId { get; set; }

    internal MethodsGroup MethodGroupToWait { get; set; }
    internal int MethodGroupToWaitId { get; set; }

    [NotMapped]
    internal MethodData MethodData { get; set; }

    //todo:remove unmapped props
    [NotMapped]
    public object Input { get; internal set; }

    [NotMapped]
    public object Output { get; internal set; }

    [NotMapped]
    public MatchExpressionParts MatchExpressionParts { get; protected set; }

    internal bool ExecuteAfterMatchAction()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AfterMatchAction)) return true;
            InvokeCallback(AfterMatchAction, Input, Output);
            FunctionState.StateObject = CurrentFunction;
            FunctionState.AddLog($"After wait [{Name}] action executed.", LogType.Info, StatusCodes.WaitProcessing);
            return true;
        }
        catch (Exception ex)
        {
            var error = $"An error occurred when try to execute action after wait [{Name}] matched.";
            FunctionState.AddError(error, StatusCodes.WaitProcessing, ex);
            //throw new Exception(error, ex);
            return false;
        }
    }
    internal override void OnAddWait()
    {
        IsRoot = ParentWait == null && ParentWaitId == null;

        if (ClosureObject == default) return;
        base.OnAddWait();
    }



    internal bool IsMatched()
    {
        try
        {
            LoadExpressions();
            if (WasFirst && MatchExpression == null)
                return true;
            if (MethodToWait.MethodInfo ==
                CoreExtensions.GetMethodInfo<LocalRegisteredMethods>(x => x.TimeWait))
                return true;
            var check = MatchExpression.CompileFast();
            var closureType = MatchExpression.Parameters[3].Type;
            var closure = GetClosure(closureType);
            return (bool)check.DynamicInvoke(Input, Output, CurrentFunction, closure);
        }
        catch (Exception ex)
        {
            var error = $"An error occurred when try evaluate match expression for wait [{Name}].";
            FunctionState.AddError(error, StatusCodes.WaitProcessing, ex);
            throw new Exception(error, ex);
        }
    }

    internal override void Cancel()
    {
        try
        {
            if (CancelMethodAction != null)
            {
                InvokeCallback(CancelMethodAction);
                CurrentFunction.AddLog($"Execute cancel method for wait [{Name}]", LogType.Info, StatusCodes.WaitProcessing);
            }
            base.Cancel();
        }
        catch (Exception ex)
        {
            var error = $"An error occurred when try to execute cancel action when wait [{Name}] canceled.";
            FunctionState.AddError(error, StatusCodes.WaitProcessing, ex);
            throw new Exception(error, ex);
        }
    }


    internal override bool ValidateWaitRequest()
    {
        if (WasFirst is false && MatchExpression == null)
        {
            FunctionState.AddError(
                    $"You didn't set the [{nameof(MatchExpression)}] for wait [{Name}]," +
                    $"This will lead to no match for any call," +
                    $"You can use method {nameof(MethodWait<int, int>.MatchIf)}(Expression<Func<TInput, TOutput, bool>> value) to pass the [{nameof(MatchExpression)}]," +
                    $"or use [{nameof(MethodWait<int, int>.MatchAny)}()] method.", StatusCodes.WaitValidation, null);
        }
        else if (WasFirst is true && MatchExpression == null)
        {
            FunctionState.AddLog(
                            $"You didn't set the [{nameof(MatchExpression)}] for first wait [{Name}]," +
                            $"This will lead to all calls will be matched.",
                            LogType.Warning, StatusCodes.WaitValidation);
        }

        if (AfterMatchAction == null)
            FunctionState.AddLog(
                $"You didn't called the method [{nameof(MethodWait<int, int>.AfterMatch)}] for wait [{Name}], " +
                $"Please use [{nameof(MethodWait<int, int>.NoActionAfterMatch)}()] if this is intended.", LogType.Warning, StatusCodes.WaitValidation);

        return base.ValidateWaitRequest();
    }

    internal void LoadExpressions()
    {
        CurrentFunction = (ResumableFunctionsContainer)FunctionState.StateObject;

        if (Template == null) return;
        Template.LoadUnmappedProps();
        MatchExpression = Template.MatchExpression;
        AfterMatchAction = Template.AfterMatchAction;
        CancelMethodAction = Template.CancelMethodAction;
    }

    public override void CopyCommonIds(WaitEntity oldWait)
    {
        base.CopyCommonIds(oldWait);
        if (oldWait is MethodWaitEntity mw)
        {
            TemplateId = mw.TemplateId;
            MethodToWaitId = mw.MethodToWaitId;
        }
    }


}

public class MethodWaitEntity<TInput, TOutput> : MethodWaitEntity
{

    internal MethodWaitEntity(Func<TInput, Task<TOutput>> method) => Initiate(method.Method);
    internal MethodWaitEntity(Func<TInput, TOutput> method) => Initiate(method.Method);
    internal MethodWaitEntity(MethodInfo methodInfo) => Initiate(methodInfo);

    private void Initiate(MethodInfo method)
    {
        var methodAttribute =
            method.GetCustomAttribute(typeof(PushCallAttribute));

        if (methodAttribute == null)
            throw new Exception(
                $"You must add attribute [{nameof(PushCallAttribute)}] to method [{method.GetFullName()}]");

        MethodData = new MethodData(method);
        Name = $"#Wait Method `{method.Name}`";
    }

    internal MethodWaitEntity<TInput, TOutput> AfterMatch(Action<TInput, TOutput> afterMatchAction)
    {
        AfterMatchAction = ValidateCallback(afterMatchAction, nameof(AfterMatchAction));
        return this;
    }

    internal MethodWaitEntity<TInput, TOutput> MatchIf(Expression<Func<TInput, TOutput, bool>> matchExpression)
    {
        MatchExpression = matchExpression;
        MatchExpressionParts = new MatchExpressionWriter(MatchExpression, CurrentFunction).MatchExpressionParts;
        if (ClosureObject != null &&
            MatchExpressionParts.Closure != null &&
            ClosureObject.GetType() != MatchExpressionParts.Closure.GetType())
            throw new Exception(
                $"For wait [{Name}] the closure must be same for AfterMatchAction,CancelAction and MatchExpression.");
        SetClosureObject(MatchExpressionParts.Closure);
        MandatoryPart = MatchExpressionParts.GetInstanceMandatoryPart(CurrentFunction);
        return this;
    }



    internal MethodWaitEntity<TInput, TOutput> WhenCancel(Action cancelAction)
    {
        CancelMethodAction = ValidateCallback(cancelAction, nameof(CancelMethodAction));
        return this;
    }

    internal MethodWaitEntity<TInput, TOutput> MatchAny()
    {
        MatchExpression = (Expression<Func<TInput, TOutput, bool>>)((_, _) => true);
        MatchExpressionParts = new MatchExpressionWriter(MatchExpression, CurrentFunction).MatchExpressionParts;
        return this;
    }

    internal MethodWaitEntity<TInput, TOutput> NoActionAfterMatch()
    {
        AfterMatchAction = string.Empty;
        return this;
    }

    internal MethodWait<TInput, TOutput> ToMethodWait() => new MethodWait<TInput, TOutput>(this);
}