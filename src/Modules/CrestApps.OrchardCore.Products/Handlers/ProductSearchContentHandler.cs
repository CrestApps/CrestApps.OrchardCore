using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Products.Handlers;

/// <summary>
/// Refreshes a product's search record when it is published, unpublished, deleted or imported, so a listing shows
/// what is live without waiting for a rebuild.
/// </summary>
/// <remarks>
/// The handler only notes which products changed. Their records are refreshed once the current scope has
/// completed, in a scope of their own: reading the index from inside a content handler flushes the session in the
/// middle of the caller's work, which breaks a recipe or deployment import half way through its batch. A separate
/// scope also keeps a failed refresh from rolling back the content change itself, and it means the handler never
/// takes the indexer as a dependency, which would be circular through <see cref="IContentManager"/>.
/// </remarks>
public sealed class ProductSearchContentHandler : ContentHandlerBase
{
    private HashSet<string> _pending;

    public override Task PublishedAsync(PublishContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task ImportedAsync(ImportContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task UnpublishedAsync(PublishContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context)
        => TrackAsync(context.ContentItem);

    private Task TrackAsync(ContentItem contentItem)
    {
        if (!contentItem.Has<ProductPart>() || string.IsNullOrEmpty(contentItem.ContentItemId))
        {
            return Task.CompletedTask;
        }

        if (_pending is null)
        {
            var pending = _pending = new HashSet<string>(StringComparer.Ordinal);

            ShellScope.AddDeferredTask(scope => RefreshAsync(scope.ServiceProvider, pending));
        }

        _pending.Add(contentItem.ContentItemId);

        return Task.CompletedTask;
    }

    private static async Task RefreshAsync(IServiceProvider services, HashSet<string> contentItemIds)
    {
        var contentManager = services.GetRequiredService<IContentManager>();
        var indexer = services.GetRequiredService<ProductSearchIndexer>();

        foreach (var contentItemId in contentItemIds)
        {
            var published = await contentManager.GetAsync(contentItemId, VersionOptions.Published);

            if (published is not null && published.Has<ProductPart>())
            {
                await indexer.IndexAsync(published);
            }
            else
            {
                await indexer.RemoveAsync(contentItemId);
            }
        }
    }
}
