using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.Implementation;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Helpers
{
    public static class Extensions
    {
        private static IServiceProvider _serviceProvider;

        public static void AddResumableFunctionsPublisher(this IServiceCollection services, IPublisherSettings settings)
        {
            services.AddSingleton<IFailedRequestHandler, FailedRequestHandler>();
            //services.AddSingleton<IFailedRequestRepo, InMemoryFailedRequestRepo>();
            services.AddSingleton<IFailedRequestRepo, OnDiskFailedRequestRepo>();
            services.AddSingleton(typeof(IPublisherSettings), settings);
            services.AddHttpClient();
            services.AddSingleton(typeof(ICallPublisher), settings.CallPublisherType);
        }

        public static void UseResumableFunctionsPublisher(this IHost app)
        {
            _serviceProvider = app.Services;
            var failedRequestsHandler = app.Services.GetService<IFailedRequestHandler>();
            failedRequestsHandler.HandleFailedRequests();
        }

        public static object GetInstance(Type type)
        {
            if (_serviceProvider == null) return null;
            return _serviceProvider.GetService(type) ??
                ActivatorUtilities.CreateInstance(_serviceProvider, type);
        }

        public static bool IsAsyncMethod(this MethodBase method)
        {
            var asyncAttr = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute));

            if (asyncAttr == null)
            {
                return
                  asyncAttr == null &&
                  method is MethodInfo mi &&
                  mi != null &&
                  mi.ReturnType.IsGenericType &&
                  mi.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
            }
            return true;
        }


        public static string GetFullName(this MethodBase method)
        {
            return $"{method.DeclaringType.FullName}.{method.Name}";
        }
    }
}