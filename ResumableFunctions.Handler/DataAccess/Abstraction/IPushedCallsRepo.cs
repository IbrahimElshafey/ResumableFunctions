﻿using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess.Abstraction;

public interface IPushedCallsRepo
{
    Task<PushedCall> GetById(long pushedCallId);
    Task Push(PushedCall pushedCall);
    Task<bool> PushedCallMatchedForFunctionBefore(long pushedCallId, int rootFunctionId);
}