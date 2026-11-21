using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// The dashboard's AI calls: listing, following, coaching, taking over and ending the calls an AI voice agent holds. They
/// need the AI services (profiles and chat sessions), so they are mapped only with the AI feature; mapped without it,
/// their handlers can't be bound and the tenant's whole pipeline fails to build.
/// </summary>
[Feature(ContactCenterConstants.Feature.Supervision)]
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class SupervisionAICallsStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddSupervisorAICallsEndpoints();
}
