using Serilog;
using Rosbank.DRPZ.WAppAutomation.Worker;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();
        config
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile("appsettings.Development.json", true, true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        services.AddApplicationServices();
        services.AddHostedService<WAppClientCallWorker>();
        
    })
    .UseSerilog((context, services, configuration) =>
    {
        configuration
            .Enrich.WithProperty("ApplicationName", "Rosbank.DRPZ.WAppAutomation")
            #if DEBUG
            .MinimumLevel.Debug()
            .WriteTo.Console()
            #endif
            .WriteTo.File($"{AppDomain.CurrentDomain.BaseDirectory}/logs/.log", rollingInterval: RollingInterval.Day);
    })
    .Build();

await host.RunAsync();
