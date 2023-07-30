﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts;
public class MethodWait : Wait
{
    internal MethodWait()
    {

    }

    [NotMapped]
    public string AfterMatchAction { get; protected set; }

    [NotMapped]
    public LambdaExpression MatchExpression { get; protected set; }

    public string CancelMethodAction { get; protected set; }

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

    [NotMapped]
    public object Input { get; set; }

    [NotMapped]
    public object Output { get; set; }
    public int InCodeLine { get; internal set; }

    public bool ExecuteAfterMatchAction()
    {
        try
        {
            if (AfterMatchAction == null) return true;
            var classType = CurrentFunction.GetType();
            var method =
                classType.GetMethod(AfterMatchAction, Flags());

            if (method == null)
                throw new NullReferenceException(
                    $"Can't find method [{AfterMatchAction}] to be executed after" +
                    $"matched wait [{Name}] in class [{classType.Name}]");

            method.Invoke(CurrentFunction, new object[] { Input, Output });
            FunctionState.StateObject = CurrentFunction;
            FunctionState.AddLog($"After wait [{Name}] action executed.", LogType.Info, StatusCodes.WaitProcessing);
            return true;
        }
        catch (Exception ex)
        {
            var error = $"An error occurred when try to execute action after wait [{Name}] matched." + ex.Message;
            FunctionState.AddLog(error, LogType.Error, StatusCodes.WaitProcessing);
            throw new Exception(error, ex);
        }
    }

    protected BindingFlags Flags() => BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public bool IsMatched()
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
            return (bool)check.DynamicInvoke(Input, Output, CurrentFunction)!;
        }
        catch (Exception ex)
        {
            var error = $"An error occurred when try evaluate match expression for wait [{Name}]." +
                        ex.Message;
            FunctionState.AddError(error, StatusCodes.WaitProcessing, ex);
            throw new Exception(error, ex);
        }
    }

    internal override void Cancel()
    {
        //call cancel method
        if (CancelMethodAction != null)
        {
            var classType = CurrentFunction.GetType();
            var method =
                classType.GetMethod(CancelMethodAction, Flags());
            var instance = classType == CurrentFunction.GetType() ? CurrentFunction : Activator.CreateInstance(classType);
            method.Invoke(instance, null);
            CurrentFunction?.AddLog($"Execute cancel method for wait [{Name}]", LogType.Info, StatusCodes.WaitProcessing);
        }
        base.Cancel();
    }

    internal override bool IsValidWaitRequest()
    {
        if (IsReplay)
            return true;
        switch (WasFirst)
        {
            case false when MatchExpression == null:
                FunctionState.AddError(
                    $"You didn't set the [{nameof(MatchExpression)}] for wait [{Name}] that is not a first wait," +
                    $"This will lead to no match for all calls," +
                    $"You can use method MatchIf(Expression<Func<TInput, TOutput, bool>> value) to pass the [{nameof(MatchExpression)}]," +
                    $"or use [MatchAll()] method.", StatusCodes.WaitValidation, null);
                break;
            case true when MatchExpression == null:
                FunctionState.AddLog(
                    $"You didn't set the [{nameof(MatchExpression)}] for first wait [{Name}]," +
                    $"This will lead to all calls will be matched.",
                    LogType.Warning, StatusCodes.WaitValidation);
                break;
        }

        if (AfterMatchAction == null)
            FunctionState.AddLog(
                $"You didn't set the [{nameof(AfterMatchAction)}] for wait [{Name}], " +
                $"Please use [NothingAfterMatch()] if this is intended.", LogType.Warning, StatusCodes.WaitValidation);

        return base.IsValidWaitRequest();
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

    public override void CopyCommonIds(Wait oldWait)
    {
        base.CopyCommonIds(oldWait);
        if (oldWait is MethodWait mw)
        {
            TemplateId = mw.TemplateId;
            MethodToWaitId = mw.MethodToWaitId;
        }

    }
}

public class MethodWait<TInput, TOutput> : MethodWait
{
    internal MethodWait(Func<TInput, Task<TOutput>> method) => Initiate(method.Method);
    internal MethodWait(Func<TInput, TOutput> method) => Initiate(method.Method);
    internal MethodWait(MethodInfo methodInfo) => Initiate(methodInfo);

    private void Initiate(MethodInfo method)
    {
        var methodAttribute =
            method.GetCustomAttribute(typeof(PushCallAttribute));

        if (methodAttribute == null)
            throw new Exception(
                $"You must add attribute [{nameof(PushCallAttribute)}] to method [{method.GetFullName()}]");

        MethodData = new MethodData(method);
        Name = $"#{method.Name}#";
    }

    public MethodWait<TInput, TOutput> AfterMatch(Action<TInput, TOutput> afterMatchAction)
    {
        var instanceType = CurrentFunction.GetType();
        if (afterMatchAction.Method.DeclaringType != instanceType)
            throw new Exception(
                $"For wait [{Name}] the [{nameof(AfterMatchAction)}] must be a method in class " +
                $"[{instanceType.Name}] or inline lambda method.");
        var hasOverload = instanceType.GetMethods(Flags()).Count(x => x.Name == afterMatchAction.Method.Name) > 1;
        if (hasOverload)
            throw new Exception(
                $"For wait [{Name}] the [CancelMethod:{afterMatchAction.Method.Name}] must not be over-loaded.");

        AfterMatchAction = afterMatchAction.Method.Name;
        return this;
    }

    public MethodWait<TInput, TOutput> MatchIf(Expression<Func<TInput, TOutput, bool>> value)
    {
        MatchExpression = value;
        return this;
    }

    public MethodWait<TInput, TOutput> WhenCancel(Action cancelAction)
    {
        var instanceType = CurrentFunction.GetType();
        if (cancelAction.Method.DeclaringType != instanceType)
            throw new Exception(
                $"For wait [{Name}] the [CancelMethod] must be a method in class " +
                $"[{instanceType.Name}] or inline lambda method.");
        var hasOverload = instanceType.GetMethods(Flags()).Count(x => x.Name == cancelAction.Method.Name) > 1;
        if (hasOverload)
            throw new Exception(
                $"For wait [{Name}] the [CancelMethod:{cancelAction.Method.Name}] must not be over-loaded.");

        CancelMethodAction = cancelAction.Method.Name;
        return this;
    }

    public MethodWait<TInput, TOutput> MatchAll()
    {
        MatchExpression = (Expression<Func<TInput, TOutput, bool>>)((x, y) => true);
        return this;
    }

    public MethodWait<TInput, TOutput> NothingAfterMatch()
    {
        AfterMatchAction = null;
        return this;
    }

}