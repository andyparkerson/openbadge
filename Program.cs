using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenBadge.Emitters;
using OpenBadge.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register HTTP client for external requests
        services.AddHttpClient();

        // Register services
        services.AddSingleton<PngBadgeBaker>();
        services.AddSingleton<PublishingService>();
        
        // Register emitters (currently using OB 2.0)
        services.AddSingleton<IStandardEmitter, Ob20Emitter>();
        
        // Alternative: Register both emitters with factory pattern (for future use)
        // services.AddSingleton<Ob20Emitter>();
        // services.AddSingleton<Ob30Emitter>();
    })
    .Build();

host.Run();
