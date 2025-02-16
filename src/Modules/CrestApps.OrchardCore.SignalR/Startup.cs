using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.SignalR.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.SignalR;

public sealed class Startup : StartupBase
{
    private static readonly JsonStringEnumConverter _enumConverter = new();

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(_enumConverter);
            });

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }
}

