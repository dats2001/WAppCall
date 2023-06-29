using Rosbank.DRPZ.WAppAutomation.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;
public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ISipClient, SipClient>();
        services.AddSingleton<IWAppDesktopClient, WAppDesktopClient>();

        return services;
    }
}
