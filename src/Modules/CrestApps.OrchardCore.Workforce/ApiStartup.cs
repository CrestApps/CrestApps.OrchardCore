using CrestApps.OrchardCore.Workforce.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Maps the Workforce HTTP API.
/// </summary>
[Feature(WorkforceConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapWorkforceApi();
    }
}
