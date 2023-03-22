﻿using ResumableFunctions.Core.InOuts;

namespace ResumableFunctions.Core.Abstraction
{
    public interface IWaitMatchedHandler
    {
        void WaitMatched(int waitId, int pushedMethodId);
        Task ProcessPushedMethod(PushedMethod pushedMethod);
    }
}
