using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.FileSources;

/// <summary>
/// Maps the file sources API when the AI Chat WebAPI feature is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class FileSourcesApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAISourceCatalogApi<FileSource>("file-sources", "AIFileSource", "file source", FileSourcePermissions.ManageFileSources);
}
