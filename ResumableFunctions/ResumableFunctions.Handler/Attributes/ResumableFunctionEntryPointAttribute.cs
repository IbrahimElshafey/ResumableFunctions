namespace ResumableFunctions.Handler.Attributes;

/// <summary>
///     Start point for a resumable function
/// </summary>
/// 
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]

public sealed class ResumableFunctionEntryPointAttribute : Attribute, ITrackingIdentifier
{
    public override object TypeId => "6d68b97e-b8fe-4550-ad7e-5056022ff81a";
    public string MethodUrn { get; }
    public bool IsActive { get; }

    public ResumableFunctionEntryPointAttribute(string methodUrn, bool isActive = true)
    {
        MethodUrn = methodUrn;
        IsActive = isActive;
    }
}