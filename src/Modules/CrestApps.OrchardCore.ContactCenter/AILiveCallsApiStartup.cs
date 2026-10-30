using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Endpoints.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Maps the live AI calls API when the supervisor dashboard and the AI Chat WebAPI feature are both on.
/// </summary>
[Feature(ContactCenterConstants.Feature.Supervision)]
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class AILiveCallsApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAILiveCallsApi();
}

/// <summary>
/// Maps the call recordings API when call recordings and the AI Chat WebAPI feature are both on.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class CallRecordingsApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapCallRecordingsApi();
}
