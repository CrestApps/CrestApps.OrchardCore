using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Maps the AI calls and texts API when the AI Chat WebAPI feature is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class AIActivitiesApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAIActivitiesApi();
}

/// <summary>
/// Maps the phone numbers API when channel addresses and the AI Chat WebAPI feature are both on.
/// </summary>
[RequireFeatures(OmnichannelConstants.Features.ChannelEndpoints, AIConstants.Feature.ChatApi)]
public sealed class ChannelEndpointsApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapChannelEndpointsApi();
}
