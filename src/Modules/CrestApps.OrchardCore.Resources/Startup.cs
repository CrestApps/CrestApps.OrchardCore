using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Resources.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Resources;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
        services.AddRecordDirectory();
        services.AddPartyDirectory();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddRecordSearchEndpoint();
        routes.AddPartySearchEndpoint();
    }
}
