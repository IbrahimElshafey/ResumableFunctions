using AspectInjector.Broker;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Reflection;

namespace ResumableFunctions.Handler.Attributes
{
    [Aspect(Scope.PerInstance, Factory = typeof(HangfireActivator))]
    public class PushCallAspect
    {
        private PushedCall _pushedCall;
        private readonly ICallPusher _callPusher;
        private readonly ILogger<PushCallAspect> _logger;
        public PushCallAspect(ICallPusher callPusher, ILogger<PushCallAspect> logger)
        {
            _callPusher = callPusher;
            _logger = logger;
        }

        [Advice(Kind.Before)]
        public void OnEntry(
            [Argument(Source.Arguments)] object[] args,
            [Argument(Source.Metadata)] MethodBase metadata,
            [Argument(Source.Triggers)] Attribute[] triggers
            )
        {
            var pushResultAttribute = triggers.OfType<PushCallAttribute>().First();

            if (string.IsNullOrWhiteSpace(pushResultAttribute.MethodUrn))
                throw new Exception(
                        $"For method [{metadata.GetFullName()}] MethodUrn must not be empty for attribute [{nameof(PushCallAttribute)}]");
            if (args.Length > 1)
                throw new Exception(
                    $"You can't apply attribute [{nameof(PushCallAttribute)}] to method " +
                    $"[{metadata.GetFullName()}] since it takes more than one parameter.");
            if (metadata is MethodInfo mi && mi.ReturnType == typeof(void))
                throw new Exception(
                    $"You can't apply attribute [{nameof(PushCallAttribute)}] to method " +
                    $"[{metadata.GetFullName()}] since return type is void, you can change it to object and return null.");
            _pushedCall = new PushedCall
            {
                MethodData = new MethodData(metadata as MethodInfo)
                {
                    MethodUrn = pushResultAttribute.MethodUrn,
                    CanPublishFromExternal = pushResultAttribute.FromExternal,
                    IsLocalOnly = pushResultAttribute.IsLocalOnly,
                },
            };
            if (args.Length > 0)
                _pushedCall.Data.Input = args[0];

        }

        [Advice(Kind.After)]
        public void OnExit(
           [Argument(Source.Name)] string name,
           [Argument(Source.ReturnValue)] object result
           )
        {
            try
            {
                _pushedCall.Data.Output = result;
                _callPusher.PushCall(_pushedCall).Wait();//local push in RF shared group
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when try to push call for method [{name}]");
            }
        }
    }
}