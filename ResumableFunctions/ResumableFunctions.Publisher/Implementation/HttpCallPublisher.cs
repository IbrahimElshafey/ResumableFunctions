using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.Helpers;
using ResumableFunctions.Publisher.InOuts;
using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Implementation
{
    public class HttpCallPublisher : ICallPublisher
    {
        private readonly IPublisherSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpCallPublisher> _logger;
        private readonly IFailedRequestHandler _failedRequestHandler;

        public HttpCallPublisher(
            IPublisherSettings settings,
            IHttpClientFactory httpClientFactory,
            ILogger<HttpCallPublisher> logger,
            IFailedRequestHandler failedRequestHandler)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _failedRequestHandler = failedRequestHandler;
        }

        public async Task Publish<TInput, TOutput>(Func<TInput, Task<TOutput>> methodToPush,
            TInput input,
            TOutput output,
            string methodUrn,
            params string[] toServices)
        {
            var minfo = methodToPush.Method;
            await Publish(new MethodCall
            {
                MethodData = new MethodData
                {
                    MethodUrn = methodUrn,
                    AssemblyName = "[From Client] " + Assembly.GetEntryAssembly()?.GetName().Name,
                    ClassName = minfo.DeclaringType.Name,
                    MethodName = minfo.Name,
                },
                Input = input,
                Output = output,
                ToServices = toServices
            });
        }

        public async Task Publish(MethodCall methodCall)
        {
            //D:\GAFT\ResumableFunctions\ResumableFunctions.AspNetService\ResumableFunctionsController.cs
            foreach (var service in methodCall.ToServices)
            {
                var serviceUrl = _settings.ServicesRegistry[service];
                string actionUrl =
                    $"{serviceUrl}{Constants.ResumableFunctionsControllerUrl}/{Constants.ExternalCallAction}";
                byte[] body = null;
                try
                {
                    methodCall.ServiceName = service;
                    body = MessagePackSerializer.Serialize(methodCall, ContractlessStandardResolver.Options);
                    var client = _httpClientFactory.CreateClient();
                    var response = await client.PostAsync(actionUrl, new ByteArrayContent(body));
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadAsStringAsync();
                    if (!(result == "1" || result == "-1"))
                        throw new Exception("Expected result must be 1 or -1");
                }
                catch (Exception ex)
                {
                    var failedRequest = new FailedRequest(Guid.NewGuid(), DateTime.UtcNow, actionUrl, body);
                    _logger.LogError(ex, $"Error occurred when publish method call {methodCall}");
                    _ = _failedRequestHandler.EnqueueFailedRequest(failedRequest);
                }
            }
        }
    }
}
