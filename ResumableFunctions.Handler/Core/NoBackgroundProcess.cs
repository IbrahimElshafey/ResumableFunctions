﻿using System.Linq.Expressions;
using FastExpressionCompiler;
using Hangfire.Annotations;
using ResumableFunctions.Handler.Core.Abstraction;

namespace ResumableFunctions.Handler.Core;

internal class NoBackgroundProcess : IBackgroundProcess
{
    public void AddOrUpdateRecurringJob([NotNull] string recurringJobId, [InstantHandle, NotNull] Expression<Func<Task>> methodCall, [NotNull] Func<string> cronExpression)
    {
        throw new NotImplementedException();
    }

    public bool Delete([NotNull] string jobId)
    {
        return true;
    }

    public string Enqueue([InstantHandle, NotNull] Expression<Func<Task>> methodCall)
    {
        try
        {
            var compiled = methodCall.CompileFast();
            compiled.Invoke().Wait();
            return default;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return default;
        }
    }

    public string Schedule([InstantHandle, NotNull] Expression<Func<Task>> methodCall, TimeSpan delay)
    {
        try
        {
            Task.Delay(delay).ContinueWith(x => methodCall.CompileFast().Invoke().Wait());
            return default;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return default;
        }
    }

    public string Schedule([InstantHandle, NotNull] Expression<Action> methodCall, TimeSpan delay)
    {
        try
        {
            Task.Delay(delay).ContinueWith(x => methodCall.CompileFast().Invoke());
            return default;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return default;
        }
    }
}
