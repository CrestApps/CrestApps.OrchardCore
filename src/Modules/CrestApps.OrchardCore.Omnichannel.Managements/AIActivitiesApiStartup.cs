using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Maps the AI calls and texts API when activities and the AI Chat WebAPI feature are both on.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class AIActivitiesApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddScoped<AIActivityStarter>();

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAIActivitiesApi();
}

/// <summary>
/// Maps the phone numbers API when channel addresses and the AI Chat WebAPI feature are both on.
/// </summary>
[Feature(OmnichannelConstants.Features.ChannelEndpoints)]
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class ChannelEndpointsApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapChannelEndpointsApi();
}
