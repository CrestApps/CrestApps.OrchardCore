using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

internal sealed class DataSourceAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DataSourceAICompletionContextBuilderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is AIProfile profile &&
            profile.TryGet<AIProfileDataSourceMetadata>(out var dataSourceMetadata) &&
            !string.IsNullOrEmpty(dataSourceMetadata.DataSourceId))
        {
            context.Context.DataSourceId = dataSourceMetadata.DataSourceId;

            // Store DataSourceId in HttpContext.Items so the DataSourceSearchTool can access it.
            var httpContext = _httpContextAccessor.HttpContext;
            httpContext?.Items["DataSourceId"] = dataSourceMetadata.DataSourceId;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
