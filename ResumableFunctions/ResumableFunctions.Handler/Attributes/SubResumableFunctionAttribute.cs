namespace ResumableFunctions.Handler.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SubResumableFunctionAttribute : Attribute, ITrackingIdentifier
{

    public override object TypeId => "dadf0bab-d93e-4435-b70a-07ee8a9a3e2f";
    public string MethodUrn { get; }

    public SubResumableFunctionAttribute(string methodUrn)
    {
        MethodUrn = methodUrn;
    }
}