﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using ResumableFunctions.Core.Helpers;

namespace ResumableFunctions.Core.InOuts;

public class WaitsGroup : Wait
{
    public WaitsGroup()
    {
        WaitType = WaitType.GroupWaitAll;
    }
    private LambdaExpression _countExpression;
    [NotMapped]
    public LambdaExpression CountExpression
    {
        get => _countExpression ?? GetCountExpression();
        internal set => _countExpression = value;
    }
    internal byte[] CountExpressionValue { get; set; }
    public int CompletedCount => ChildWaits?.Count(x => x.Status == WaitStatus.Completed) ?? 0;

    public override bool IsFinished()
    {

        var isFinished = false;
        switch (WaitType)
        {
            case WaitType.GroupWaitAll:
                isFinished = ChildWaits?.All(x => x.Status == WaitStatus.Completed) is true;
                break;
            case WaitType.GroupWaitFirst:
                isFinished = ChildWaits?.Any(x => x.Status == WaitStatus.Completed) is true;
                break;
            case WaitType.GroupWaitWithExpression when CountExpression != null:
                {
                    var matchCompiled = (Func<WaitsGroup, bool>)CountExpression.Compile();
                    var isCompleted = matchCompiled(this);
                    Status = isCompleted ? WaitStatus.Completed : Status;
                    return isCompleted;
                }
            case WaitType.GroupWaitWithExpression:
                isFinished = ChildWaits?.Any(x => x.Status == WaitStatus.Waiting) is false;
                break;
        }
        return isFinished;
    }



    private LambdaExpression GetCountExpression()
    {
        //todo:may be a bug
        var assembly =
            RequestedByFunction.MethodInfo.DeclaringType.Assembly ??
            Assembly.GetEntryAssembly();
        if (CountExpressionValue != null)
            return (LambdaExpression)
                ExpressionToJsonConverter.JsonToExpression(
                    TextCompressor.DecompressString(CountExpressionValue), assembly);
        return null;
    }

    public Wait When(Expression<Func<WaitsGroup, bool>> matchCountFilter)
    {
        WaitType = WaitType.GroupWaitWithExpression;
        var assembly = CurrentFunction?.GetType().Assembly;
        if (assembly != null)
        {
            CountExpression = matchCountFilter;
            CountExpressionValue =
                TextCompressor.CompressString(
                    ExpressionToJsonConverter.ExpressionToJson(CountExpression, assembly));
        }

        return this;
    }
    public Wait All()
    {
        WaitType = WaitType.GroupWaitAll;
        return this;
    }

    public Wait First()
    {
        WaitType = WaitType.GroupWaitFirst;
        return this;
    }
}