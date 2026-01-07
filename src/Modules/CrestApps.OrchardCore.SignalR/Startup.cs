using System.Text.Json;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.SignalR.Filters;
using CrestApps.OrchardCore.SignalR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.SignalR;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSignalRServices();

        services
            .AddSignalR(options =>
            {
                options.AddFilter<SessionHubFilter>();
            })
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
