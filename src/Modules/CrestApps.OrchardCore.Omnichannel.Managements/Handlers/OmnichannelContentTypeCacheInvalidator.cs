using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Invalidates the per-tenant omnichannel content type cache maintained by <see cref="OmnichannelContentTypeProvider"/>
/// whenever a content definition changes, so the next read recomputes fresh subject and contact membership sets.
/// This handler is intentionally separate from the provider: the provider resolves <c>IContentDefinitionManager</c>,
/// and registering the provider itself as an <see cref="IContentDefinitionEventHandler"/> would create a circular
/// dependency because resolving the manager resolves its event handlers.
/// </summary>
internal sealed class OmnichannelContentTypeCacheInvalidator : IContentDefinitionEventHandler
{
    private readonly IMemoryCache _memoryCache;
    private readonly string _cacheKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContentTypeCacheInvalidator"/> class.
    /// </summary>
    /// <param name="memoryCache">The per-tenant memory cache holding the omnichannel content type membership sets.</param>
    /// <param name="shellSettings">The shell settings used to scope the cache entry to the current tenant.</param>
    public OmnichannelContentTypeCacheInvalidator(
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _memoryCache = memoryCache;
        _cacheKey = OmnichannelContentTypeProvider.GetCacheKey(shellSettings);
    }

    /// <inheritdoc/>
    public void ContentTypeCreated(ContentTypeCreatedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentTypeUpdated(ContentTypeUpdatedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentTypeImported(ContentTypeImportedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentTypeRemoved(ContentTypeRemovedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentPartAttached(ContentPartAttachedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentPartDetached(ContentPartDetachedContext context)
        => Invalidate();

    /// <inheritdoc/>
    public void ContentTypeImporting(ContentTypeImportingContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartCreated(ContentPartCreatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartUpdated(ContentPartUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartRemoved(ContentPartRemovedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartImporting(ContentPartImportingContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartImported(ContentPartImportedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentTypePartUpdated(ContentTypePartUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldAttached(ContentFieldAttachedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldUpdated(ContentFieldUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldDetached(ContentFieldDetachedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartFieldUpdated(ContentPartFieldUpdatedContext context)
    {
    }

    private void Invalidate()
        => _memoryCache.Remove(_cacheKey);
}
