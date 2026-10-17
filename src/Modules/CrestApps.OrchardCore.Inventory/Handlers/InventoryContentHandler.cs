using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Inventory.Handlers;

/// <summary>
/// Keeps a product's stock items in step with it after it is saved, published, imported or removed.
/// </summary>
/// <remarks>
/// The handler only notes which products changed: the sync runs once the request is done, because it needs the
/// content manager (which a content handler can't depend on) and must not query the session in the middle of an
/// import.
/// </remarks>
public sealed class InventoryContentHandler : ContentHandlerBase
{
    private HashSet<string> _products;

    public override Task UpdatedAsync(UpdateContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task PublishedAsync(PublishContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task ImportedAsync(ImportContentContext context)
        => TrackAsync(context.ContentItem);

    public override Task RemovedAsync(RemoveContentContext context)
        => TrackAsync(context.ContentItem);

    private Task TrackAsync(ContentItem contentItem)
    {
        // A product without the Inventory part is tracked too, so taking the part away archives its stock items.
        if (string.IsNullOrEmpty(contentItem.ContentItemId) || !(contentItem.Has<InventoryPart>() || contentItem.Has<ProductPart>()))
        {
            return Task.CompletedTask;
        }

        if (_products is null)
        {
            var products = _products = new HashSet<string>(StringComparer.Ordinal);

            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<InventorySyncService>().SyncAsync(products));
        }

        _products.Add(contentItem.ContentItemId);

        return Task.CompletedTask;
    }
}
