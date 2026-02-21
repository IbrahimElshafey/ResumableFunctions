using FastExpressionCompiler;
using Hangfire.Annotations;
using ResumableFunctions.Handler.Core.Abstraction;
using System.Linq.Expressions;

namespace ResumableFunctions.Handler.Core;

internal class NoBackgroundProcess : IBackgroundProcess
{

    public void AddOrUpdateRecurringJob<TClass>(
        [NotNull] string recurringJobId,
        [InstantHandle, NotNull] Expression<Func<TClass, Task>> methodCall,
        [NotNull] string cronExpression)
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
            return Random.Shared.Next().ToString();
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
            return Random.Shared.Next().ToString();
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
            return Random.Shared.Next().ToString();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return default;
        }
    }
}
