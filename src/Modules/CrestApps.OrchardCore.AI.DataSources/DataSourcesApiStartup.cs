using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources;

/// <summary>
/// Maps the data sources API when the AI Chat WebAPI feature is on.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatApi)]
public sealed class DataSourcesApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAISourceCatalogApi<AIDataSource>("data-sources", "AIDataSource", "data source", AIPermissions.ManageAIDataSources);
}
