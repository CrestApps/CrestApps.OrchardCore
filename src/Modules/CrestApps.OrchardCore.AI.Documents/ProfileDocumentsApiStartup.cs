using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents;

/// <summary>
/// Maps the profile documents API when profile documents and the AI Chat WebAPI feature are both on.
/// </summary>
[Feature(AIConstants.Feature.ProfileDocuments)]
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class ProfileDocumentsApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAIProfileDocumentsApi();
}
