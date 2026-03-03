using System.Text.Json;
using CrestApps.OrchardCore.SignalR.Services;
using CrestApps.SignalR.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.SignalR;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped(sp =>
        {
            var shellSettings = sp.GetRequiredService<ShellSettings>();

            return new HubRouteManager(shellSettings.RequestUrlPrefix);
        });

        services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                foreach (var converter in JOptions.KnownConverters)
                {
                    options.PayloadSerializerOptions.Converters.Add(converter);
                }
            });

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}
