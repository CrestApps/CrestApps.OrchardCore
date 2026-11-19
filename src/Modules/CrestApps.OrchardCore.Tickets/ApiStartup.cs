using CrestApps.OrchardCore.Tickets.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// Maps the tickets JSON API, <c>api/tickets/v1</c>.
/// </summary>
[Feature(TicketsConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapTicketsApi();
}
