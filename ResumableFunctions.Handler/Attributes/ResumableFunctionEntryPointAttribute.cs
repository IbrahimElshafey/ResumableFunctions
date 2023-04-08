﻿namespace ResumableFunctions.Handler.Attributes;

/// <summary>
///     Start point for a resumable function
/// </summary>
/// 
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]

public sealed class ResumableFunctionEntryPointAttribute : Attribute, ITrackingIdetifier
{
    public const string AttributeId = nameof(ResumableFunctionEntryPointAttribute);
    public override object TypeId => AttributeId;
    public string MethodUrn { get; }

    public ResumableFunctionEntryPointAttribute(string methodUrn)
    {
        MethodUrn = methodUrn;
    }
}