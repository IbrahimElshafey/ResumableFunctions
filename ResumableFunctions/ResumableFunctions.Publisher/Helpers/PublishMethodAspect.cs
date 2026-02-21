using AspectInjector.Broker;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.InOuts;
using System;
using System.Linq;
using System.Reflection;

namespace ResumableFunctions.Publisher.Helpers
{
    [Aspect(Scope.PerInstance, Factory = typeof(Extensions))]
    public class PublishMethodAspect
    {
        private MethodCall _methodCall;
        private readonly ILogger<PublishMethodAspect> _logger;
        private readonly ICallPublisher _callPublisher;

        public PublishMethodAspect(ILogger<PublishMethodAspect> logger, ICallPublisher callPublisher)
        {
            _logger = logger;
            _callPublisher = callPublisher;
        }

        [Advice(Kind.Before)]
        public void OnEntry(
            [Argument(Source.Arguments)] object[] args,
            [Argument(Source.Metadata)] MethodBase metadata,
            [Argument(Source.Triggers)] Attribute[] triggers
            )
        {
            var publishMethodAttribute = triggers.OfType<PublishMethodAttribute>().First();
            if (string.IsNullOrWhiteSpace(publishMethodAttribute.MethodUrn))
                throw new Exception(
                        $"For method [{metadata.GetFullName()}] MethodUrn must not be empty for attribute [{nameof(PublishMethodAttribute)}]");
            if (args.Length > 1)
                throw new Exception(
                    $"You can't apply attribute [{nameof(PublishMethodAttribute)}] to method " +
                    $"[{metadata.GetFullName()}] since it takes more than one parameter.");
            if (metadata is MethodInfo mi && mi.ReturnType == typeof(void))
                throw new Exception(
                    $"You can't apply attribute [{nameof(PublishMethodAttribute)}] to method " +
                    $"[{metadata.GetFullName()}] since return type is void, you can change it to object and return null.");
            _methodCall = new MethodCall
            {
                MethodData = new MethodData
                {
                    MethodUrn = publishMethodAttribute.MethodUrn,
                    AssemblyName = "[From Client] " + Assembly.GetEntryAssembly()?.GetName().Name,
                    ClassName = metadata.DeclaringType.Name,
                    MethodName = metadata.Name,
                },
                ToServices = publishMethodAttribute.ToServices
            };
            if (args.Length > 0)
                _methodCall.Input = args[0];
        }

        [Advice(Kind.After)]
        public void OnExit(
           [Argument(Source.Name)] string name,
           [Argument(Source.ReturnValue)] object result
           )
        {
            try
            {
                _methodCall.Output = result;
                _callPublisher.Publish(_methodCall);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when try to push call for method [{name}]");
            }
        }
    }
}