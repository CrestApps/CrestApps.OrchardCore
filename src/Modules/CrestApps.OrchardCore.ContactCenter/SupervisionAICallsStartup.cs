using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// The dashboard's AI calls: listing, following, coaching, taking over and ending the calls an AI voice agent holds. They
/// need the AI profiles of AI Services and the chat sessions and their prompts of AI Chat Services, so they are mapped
/// only with AI Chat Services, which depends on AI Services; mapped without it, their handlers can't be bound and the
/// tenant's whole pipeline fails to build.
/// </summary>
[Feature(ContactCenterConstants.Feature.Supervision)]
[RequireFeatures(AIConstants.Feature.ChatCore)]
public sealed class SupervisionAICallsStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.AddSupervisorAICallsEndpoints();
}
