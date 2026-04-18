using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApimMcpDemo.Core.Implementations;
using ApimMcpDemo.Core.Interfaces;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register InMemoryDataStore as singleton so state is shared across all function invocations
        services.AddSingleton<IDataStore, InMemoryDataStore>();

        // Configure JSON serialization with camelCase naming
        services.Configure<JsonSerializerOptions>(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.WriteIndented = false;
        });
    })
    .Build();

host.Run();
