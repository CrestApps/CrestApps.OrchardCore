using CrestApps.OrchardCore.Scheduling.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Maps the scheduling HTTP API.
/// </summary>
[Feature(SchedulingConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapSchedulingApi();
    }
}
