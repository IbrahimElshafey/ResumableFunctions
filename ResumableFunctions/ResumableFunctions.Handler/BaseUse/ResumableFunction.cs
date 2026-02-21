using FastExpressionCompiler;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace ResumableFunctions.Handler;

public abstract partial class ResumableFunctionsContainer : IObjectWithLog
{
    public virtual Task OnError(string message, Exception ex = null)
    {
        return Task.CompletedTask;
    }
    public virtual Task OnCompleted()
    {
        return Task.CompletedTask;
    }
    [IgnoreMember] internal MethodInfo CurrentResumableFunction { get; set; }

    protected void AddInfoLog(string message) => this.AddLog(message, LogType.Info, Helpers.StatusCodes.Custom);
    protected void AddWarningLog(string message) => this.AddLog(message, LogType.Warning, Helpers.StatusCodes.Custom);
    protected void AddErrorLog(string message, Exception ex = null) => this.AddError(message, Helpers.StatusCodes.Custom, ex);

    [IgnoreMember][NotMapped] public List<LogRecord> Logs { get; set; } = new();

    public EntityType EntityType => EntityType.ServiceLog;

    //[IgnoreMember][NotMapped] internal Dictionary<string, object> Closures { get; } = new();

    private bool _dependenciesAreSet;
    internal void InitializeDependencies(IServiceProvider serviceProvider)
    {
        if (_dependenciesAreSet) return;
        var setDependenciesMi = GetType().GetMethod(
            "SetDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (setDependenciesMi == null)
            return;

        var parameters = setDependenciesMi.GetParameters();
        var inputs = new object[parameters.Length];
        var matchSignature = setDependenciesMi.ReturnType == typeof(void) && parameters.Any();
        if (matchSignature)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                inputs[i] =
                    serviceProvider.GetService(parameters[i].ParameterType) ??
                    ActivatorUtilities.CreateInstance(serviceProvider, parameters[i].ParameterType);
            }
        }
        CallSetDependencies(inputs, setDependenciesMi, parameters);
        _dependenciesAreSet = true;
    }


    private void CallSetDependencies(object[] inputs, MethodInfo mi, ParameterInfo[] parameterTypes)
    {
        var instance = Expression.Parameter(GetType(), "instance");
        var depsParams = parameterTypes.Select(x => Expression.Parameter(x.ParameterType)).ToList();
        var parameters = new List<ParameterExpression>
        {
            instance
        };
        parameters.AddRange(depsParams);
        var call = Expression.Call(instance, mi, depsParams);
        var lambda = Expression.Lambda(call, parameters);
        var compiledFunction = lambda.CompileFast();
        var paramsAll = new List<object>(inputs.Length)
        {
            this
        };
        paramsAll.AddRange(inputs);
        compiledFunction.DynamicInvoke(paramsAll.ToArray());
    }
}