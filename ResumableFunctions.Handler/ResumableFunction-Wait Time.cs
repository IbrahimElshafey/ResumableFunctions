﻿using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler;

public abstract partial class ResumableFunction
{
    protected TimeWait Wait(TimeSpan timeToWait)
    {
        return new TimeWait
        {
            Name = nameof(TimeWait),
            TimeToWait = timeToWait,
            UniqueMatchId = Guid.NewGuid().ToString(),
            CurrentFunction = this
        };
    }
    
}