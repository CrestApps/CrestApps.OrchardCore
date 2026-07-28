using System.Text.Json;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.SignalR.Middlewares;
using CrestApps.OrchardCore.SignalR.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.SignalR;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    // The hub authentication middleware must run after the authentication middleware and
    // before any module that authorizes endpoints.
    public override int ConfigureOrder
        => OrchardCoreConstants.ConfigureOrder.Authentication + 1;

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

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        app.UseMiddleware<HubApiAuthenticationMiddleware>();
    }
}
