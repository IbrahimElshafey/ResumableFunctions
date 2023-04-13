﻿namespace ResumableFunctions.Handler.InOuts;

public class MethodsGroup
{
    public int Id { get; internal set; }
    public string MethodGroupUrn { get; internal set; }
    public List<WaitMethodIdentifier> WaitMethodIdentifiers { get; internal set; } = new();
    public List<MethodWait> WaitRequestsForGroup { get; internal set; }

}
