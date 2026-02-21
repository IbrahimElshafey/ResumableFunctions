using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Runtime.CompilerServices;

namespace ResumableFunctions.Handler;

public abstract partial class ResumableFunctionsContainer
{
    protected TimeWait WaitUntil(
        DateTime untilDate,
        string name = null,
        [CallerLineNumber] int inCodeLine = 0,
        [CallerMemberName] string callerName = "")
    {
        if(untilDate < DateTime.UtcNow)
        {
            throw new ArgumentException("Until date should be in the future", nameof(untilDate));
        }
        var timeToWait = untilDate - DateTime.UtcNow;
        return new TimeWaitEntity(this)
        {
            Name = name ?? $"#Time Wait for `{timeToWait.TotalHours}` hours in `{callerName}`",
            TimeToWait = timeToWait,
            UniqueMatchId = Guid.NewGuid().ToString(),
            CurrentFunction = this,
            InCodeLine = inCodeLine,
            CallerName = callerName,
            Created = DateTime.UtcNow
        }.ToTimeWait();
    }

    protected TimeWait WaitDelay(
        TimeSpan timeToWait,
        string name = null,
        [CallerLineNumber] int inCodeLine = 0,
        [CallerMemberName] string callerName = "")
    {
        if(timeToWait.TotalMilliseconds <= 0)
        {
            throw new ArgumentException("Time to wait should be greater than 0", nameof(timeToWait));
        }
        return new TimeWaitEntity(this)
        {
            Name = name ?? $"#Time Wait for `{timeToWait.TotalHours}` hours in `{callerName}`",
            TimeToWait = timeToWait,
            UniqueMatchId = Guid.NewGuid().ToString(),
            CurrentFunction = this,
            InCodeLine = inCodeLine,
            CallerName = callerName,
            Created = DateTime.UtcNow
        }.ToTimeWait();
    }

}