using System.Text.Json;
using CrestApps.OrchardCore.SignalR.Services;
using CrestApps.SignalR.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.SignalR;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped(sp =>
        {
            var shellSettings = sp.GetRequiredService<ShellSettings>();
            var siteService = sp.GetRequiredService<ISiteService>();

            return new HubRouteManager(shellSettings.RequestUrlPrefix, () => siteService.GetSiteSettings().BaseUrl);
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
