using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Maintains cached sets of the content types that have the <c>OmnichannelSubjectPart</c> or the
/// <c>OmnichannelContactPart</c> attached so callers can answer that question, and build subject and contact
/// content type drop downs, without scanning every content type definition on each request. The sets are warmed
/// once from the content definitions into <see cref="IMemoryCache"/> and then evicted through the
/// <see cref="IContentDefinitionEventHandler"/> notifications, so they are re-read only when a definition changes.
/// </summary>
public sealed class OmnichannelContentTypeProvider : IContentDefinitionEventHandler
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ShellSettings _shellSettings;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContentTypeProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read type definitions.</param>
    /// <param name="memoryCache">The memory cache used to hold the subject and contact content type sets across requests.</param>
    public OmnichannelContentTypeProvider(
        IContentDefinitionManager contentDefinitionManager,
        ShellSettings shellSettings,
        IMemoryCache memoryCache)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _shellSettings = shellSettings;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the subject content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetSubjectContentTypesAsync()
    {
        var (subjectContentTypes, _) = await GetOrWarmAsync();

        return subjectContentTypes;
    }

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelContactPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the contact content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync()
    {
        var (_, contactContentTypes) = await GetOrWarmAsync();

        return contactContentTypes;
    }

    /// <summary>
    /// Reads the cached subject and contact content type sets, warming them from the current content definitions
    /// when either is missing from the cache.
    /// </summary>
    private async ValueTask<(HashSet<string> Subject, HashSet<string> Contact)> GetOrWarmAsync()
    {
        if (_memoryCache.TryGetValue(GetSubjectCacheKey(), out HashSet<string> subjectContentTypes) &&
            _memoryCache.TryGetValue(GetContactCacheKey(), out HashSet<string> contactContentTypes))
        {
            return (subjectContentTypes, contactContentTypes);
        }

        subjectContentTypes = new HashSet<string>(StringComparer.Ordinal);
        contactContentTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(definition))
            {
                subjectContentTypes.Add(definition.Name);
            }

            if (OmnichannelContactDefinitionService.HasOmnichannelContactPart(definition))
            {
                contactContentTypes.Add(definition.Name);
            }
        }

        _memoryCache.Set(GetSubjectCacheKey(), subjectContentTypes);
        _memoryCache.Set(GetContactCacheKey(), contactContentTypes);

        return (subjectContentTypes, contactContentTypes);
    }

    private string GetSubjectCacheKey()
        => $"{_shellSettings.Name}:CrestApps:Omnichannel:SubjectContentTypes";

    private string GetContactCacheKey()
        => $"{_shellSettings.Name}:CrestApps:Omnichannel:ContactContentTypes";

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
    {
        if (IsSubjectPart(context.ContentPartName) || IsContactPart(context.ContentPartName))
        {
            Invalidate();
        }
    }

    /// <inheritdoc/>
    public void ContentPartDetached(ContentPartDetachedContext context)
    {
        if (IsSubjectPart(context.ContentPartName) || IsContactPart(context.ContentPartName))
        {
            Invalidate();
        }
    }

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
    {
        _memoryCache.Remove(GetSubjectCacheKey());
        _memoryCache.Remove(GetContactCacheKey());
    }

    private static bool IsSubjectPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelSubject, StringComparison.Ordinal);

    private static bool IsContactPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal);
}
