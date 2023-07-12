using Rosbank.DRPZ.WAppAutomation.Application.Services;
using Rosbank.DRPZ.WAppAutomation.Domain.Interfaces;

namespace Microsoft.Extensions.DependencyInjection;
public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        //services.AddSingleton<ISipClient, SipClient>();
        services.AddSingleton<IWAppDesktopClient, WAppDesktopClient>();
        services.AddSingleton<ICallBroker, CallBroker>();

        return services;
    }
}
