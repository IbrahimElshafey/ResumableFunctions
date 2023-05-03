﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.RateLimiting;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts;

public class MethodWait : Wait
{
    public int PushedCallId { get; internal set; }
    [NotMapped] public LambdaExpression SetDataExpression { get; internal set; }

    internal byte[] SetDataExpressionValue { get; set; }

    [NotMapped]
    public LambdaExpression MatchIfExpression { get; internal set; }

    internal byte[] MatchIfExpressionValue { get; set; }

    public bool NeedFunctionStateForMatch { get; internal set; } = false;

    public string RefineMatchModifier { get; internal set; }


    /// <summary>
    ///     The method that we wait to resume resumable function
    /// </summary>
    internal WaitMethodIdentifier MethodToWait { get; set; }
    internal int MethodToWaitId { get; set; }

    internal MethodsGroup MethodGroupToWait { get; set; }
    internal int MethodGroupToWaitId { get; set; }

    [NotMapped]
    internal MethodData MethodData { get; set; }

    [NotMapped]
    public object Input { get; set; }

    [NotMapped]
    public object Output { get; set; }

    //todo:bug
    private Assembly FunctionAssembly =>
        MethodToWait?.MethodInfo?.DeclaringType.Assembly ??
        RequestedByFunction?.MethodInfo?.DeclaringType.Assembly ??
        Assembly.GetEntryAssembly();

    internal void RewriteExpressions()
    {
        //Rewrite Match Expression
        try
        {
            MatchIfExpression = new RewriteMatchExpression(this).Result;
            MatchIfExpressionValue =
                TextCompressor.CompressString(ExpressionToJsonConverter.ExpressionToJson(MatchIfExpression, FunctionAssembly));

            //Rewrite SetData Expression
            SetDataExpression = new RewriteSetDataExpression(this).Result;
            SetDataExpressionValue =
                TextCompressor.CompressString(ExpressionToJsonConverter.ExpressionToJson(SetDataExpression, FunctionAssembly));
        }
        catch (Exception ex)
        {
            FunctionState?.AddLog(
                LogStatus.Warning,
                 $"Error happened when rewrite expressions for method wait [{Name}].\n" +
                 $"{ex.Message}\n" +
                 $"{ex.StackTrace}");
        }
    }



    internal void LoadExpressions()
    {
        MatchIfExpression = (LambdaExpression)
            ExpressionToJsonConverter.JsonToExpression(
                TextCompressor.DecompressString(MatchIfExpressionValue), FunctionAssembly);
        SetDataExpression = (LambdaExpression)
            ExpressionToJsonConverter.JsonToExpression(
                TextCompressor.DecompressString(SetDataExpressionValue), FunctionAssembly);
    }

    public bool UpdateFunctionData()
    {
        try
        {
            var setDataExpression = SetDataExpression.Compile();
            setDataExpression.DynamicInvoke(Input, Output, CurrentFunction);
            FunctionState.StateObject = CurrentFunction;
            FunctionState.AddLog(
                LogStatus.InProgress,
                $"Method wait [{Name}] matched and function data updated.");
            return true;
        }
        catch (Exception ex)
        {
            FunctionState.AddLog(
                LogStatus.Error,
                $"An error occured when try to update function data after method wait [{Name}] matched." +
                ex.Message);
            return false;
        }
    }

    public bool IsMatched()
    {
        try
        {
            if (IsFirst && MatchIfExpressionValue == null)
                return true;
            var check = MatchIfExpression.Compile();
            return (bool)check.DynamicInvoke(Input, Output, CurrentFunction);
        }
        catch (Exception e)
        {
            FunctionState.AddLog(
               LogStatus.Error,
               $"An error occured when try evaluate match for wait [{Name}]." +
               e.Message);
            return false;
        }
    }

    internal override bool IsValidWaitRequest()
    {
        //Todo:validate type serialization
        if (!IsFirst && MatchIfExpression == null)
            FunctionState.AddLog(
                LogStatus.Error,
                $"You didn't set the `MatchIfExpression` for wait [{Name}] that is not a first wait," +
                $"This will lead to no match for all calls," +
                $"You can use method MatchIf(Expression<Func<TInput, TOutput, bool>> value) to pass the `MatchIfExpression`," +
                $"or use MatchAll() method.");
        if (IsFirst && MatchIfExpression == null)
            FunctionState.AddLog(
                LogStatus.Warning,
                $"You didn't set the `MatchIfExpression` for first wait [{Name}]," +
                $"This will lead to all calls will be matched.");
        if (SetDataExpression == null)
            FunctionState.AddLog(
                LogStatus.Error,
                $"You didn't set the `SetDataExpression` for wait [{Name}], " +
                $"The execution will not continue, " +
                $"Please use `NoSetData()` if this is intended.");
        return base.IsValidWaitRequest();
    }

    internal void SetInputAndOutput()
    {
        var methodInfo = MethodToWait.MethodInfo;
        try
        {
            var inputType = methodInfo.GetParameters()[0].ParameterType;
            if (Input is JObject inputJson)
            {
                Input = inputJson.ToObject(inputType);
            }
            else
                Input = Convert.ChangeType(Input.ToString(), inputType);
        }
        catch (Exception ex)
        {

        }

        try
        {
            if (Output is JObject outputJson)
            {
                if (methodInfo.IsAsyncMethod())
                    Output = outputJson.ToObject(methodInfo.ReturnType.GetGenericArguments()[0]);
                else
                    Output = outputJson.ToObject(methodInfo.ReturnType);
            }
            else
                Output = Convert.ChangeType(Output.ToString(), methodInfo.ReturnType);
        }
        catch (Exception)
        {
        }

    }
}

public class MethodWait<TInput, TOutput> : MethodWait
{
    public MethodWait(Func<TInput, Task<TOutput>> method)
    {
        Initiate(method.Method);
    }
    public MethodWait(Func<TInput, TOutput> method)
    {
        Initiate(method.Method);
    }

    private void Initiate(MethodInfo method)
    {
        var methodAttribute =
            method.GetCustomAttribute(typeof(WaitMethodAttribute));
        if (methodAttribute == null)
            throw new Exception(
                $"You must add attribute [WaitMethod , WaitMethodImplementation or ExternalWaitMethod] to method {method.GetFullName()}");

        MethodData = new MethodData(method);
        Name = $"#{method.Name}#";
    }

    public MethodWait<TInput, TOutput> SetData(Expression<Func<TInput, TOutput, bool>> value)
    {
        SetDataExpression = value;
        return this;
    }

    public MethodWait<TInput, TOutput> MatchIf(Expression<Func<TInput, TOutput, bool>> value)
    {
        MatchIfExpression = value;
        return this;
    }

    public MethodWait<TInput, TOutput> MatchAll()
    {
        MatchIfExpression = (Expression<Func<TInput, TOutput, bool>>)((x, y) => true);
        return this;
    }

    public MethodWait<TInput, TOutput> NoSetData()
    {
        SetDataExpression = (Expression<Func<TInput, TOutput, bool>>)((x, y) => true);
        return this;
    }

}