using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace ResumableFunctions.Handler.Helpers;

public class HangfireActivator : JobActivator
{
    internal static IServiceProvider serviceProvider;
    public HangfireActivator(IServiceProvider serviceProvider)
    {
        HangfireActivator.serviceProvider = serviceProvider;
    }
    public override object ActivateJob(Type type)
    {
        return GetInstance(type);
    }

    public static object GetInstance(Type type)
    {
        if (serviceProvider == null) return null;
        var newServiceProvider = serviceProvider.CreateScope().ServiceProvider;
        return newServiceProvider.GetService(type) ??
            ActivatorUtilities.CreateInstance(newServiceProvider, type);
    }
}