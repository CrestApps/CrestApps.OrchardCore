using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Maps the carrier numbers API when the AI Chat WebAPI feature is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class NumbersApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapTelnyxNumbersApi();
}
