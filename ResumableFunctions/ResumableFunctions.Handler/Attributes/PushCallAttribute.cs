using AspectInjector.Broker;

namespace ResumableFunctions.Handler.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [Injection(typeof(PushCallAspect), Inherited = true)]
    public class PushCallAttribute : Attribute, ITrackingIdentifier
    {
        public PushCallAttribute(string methodUrn)
        {
            MethodUrn = methodUrn;
        }

        public string MethodUrn { get; }
        public bool FromExternal { get; set; }
        public bool IsLocalOnly { get; set; }

        public override object TypeId => "1f220128-d0f7-4dac-ad81-ff942d68942c";

        public override string ToString()
        {
            return $"{MethodUrn},{FromExternal}";
        }
    }
}