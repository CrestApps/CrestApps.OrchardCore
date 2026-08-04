using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reports the content types that have the <c>OmnichannelSubjectPart</c> or the <c>OmnichannelContactPart</c>
/// attached, so callers can answer that question and build subject and contact content type drop downs without
/// scanning every content type definition on each request. The membership sets are computed once and cached in
/// the per-tenant <see cref="IMemoryCache"/>, then kept current through the <see cref="IContentDefinitionEventHandler"/>
/// notifications so they always reflect the latest definitions without repeated enumeration.
/// </summary>
public sealed class OmnichannelContentTypeProvider : IContentDefinitionEventHandler
{
    private static readonly Lock _lock = new();

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IMemoryCache _memoryCache;
    private readonly string _cacheKey;

    private OmnichannelContentTypeSet _set;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContentTypeProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read the type definitions.</param>
    /// <param name="memoryCache">The per-tenant memory cache used to store the membership sets.</param>
    /// <param name="shellSettings">The shell settings used to scope the cache entry to the current tenant.</param>
    public OmnichannelContentTypeProvider(
        IContentDefinitionManager contentDefinitionManager,
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _memoryCache = memoryCache;
        _cacheKey = GetCacheKey(shellSettings);
    }

    /// <summary>
    /// Builds the per-tenant memory cache key used to store the omnichannel content type membership sets.
    /// </summary>
    /// <param name="shellSettings">The shell settings whose <see cref="ShellSettings.Name"/> scopes the entry.</param>
    /// <returns>The cache key for the tenant.</returns>
    internal static string GetCacheKey(ShellSettings shellSettings)
        => $"OmnichannelContentTypes:{shellSettings.Name}";

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a subject; otherwise, <see langword="false"/>.</returns>
    public async ValueTask<bool> IsSubjectContentTypeAsync(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var set = await EnsureInitializedAsync();

        return set.SubjectContentTypes.Contains(contentType);
    }

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelContactPart</c> attached.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a contact; otherwise, <see langword="false"/>.</returns>
    public async ValueTask<bool> IsContactContentTypeAsync(string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var set = await EnsureInitializedAsync();

        return set.ContactContentTypes.Contains(contentType);
    }

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the subject content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetSubjectContentTypesAsync()
    {
        var set = await EnsureInitializedAsync();

        return set.SubjectContentTypes;
    }

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelContactPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the contact content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync()
    {
        var set = await EnsureInitializedAsync();

        return set.ContactContentTypes;
    }

    private async ValueTask<OmnichannelContentTypeSet> EnsureInitializedAsync()
    {
        if (_set is not null)
        {
            return _set;
        }

        if (_memoryCache.TryGetValue<OmnichannelContentTypeSet>(_cacheKey, out var cached) && cached is not null)
        {
            _set = cached;

            return cached;
        }

        var subjectContentTypes = new HashSet<string>(StringComparer.Ordinal);
        var contactContentTypes = new HashSet<string>(StringComparer.Ordinal);

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

        var set = new OmnichannelContentTypeSet(subjectContentTypes, contactContentTypes);

        lock (_lock)
        {
            if (_memoryCache.TryGetValue<OmnichannelContentTypeSet>(_cacheKey, out var existing) && existing is not null)
            {
                set = existing;
            }
            else
            {
                _memoryCache.Set(_cacheKey, set);
            }
        }

        _set = set;

        return set;
    }

    /// <inheritdoc/>
    public void ContentTypeCreated(ContentTypeCreatedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeUpdated(ContentTypeUpdatedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeImported(ContentTypeImportedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeRemoved(ContentTypeRemovedContext context)
    {
        var contentType = context.ContentTypeDefinition?.Name;

        SetMembership(subject: true, contentType, isMember: false);
        SetMembership(subject: false, contentType, isMember: false);
    }

    /// <inheritdoc/>
    public void ContentPartAttached(ContentPartAttachedContext context)
    {
        if (IsSubjectPart(context.ContentPartName))
        {
            SetMembership(subject: true, context.ContentTypeName, isMember: true);
        }
        else if (IsContactPart(context.ContentPartName))
        {
            SetMembership(subject: false, context.ContentTypeName, isMember: true);
        }
    }

    /// <inheritdoc/>
    public void ContentPartDetached(ContentPartDetachedContext context)
    {
        if (IsSubjectPart(context.ContentPartName))
        {
            SetMembership(subject: true, context.ContentTypeName, isMember: false);
        }
        else if (IsContactPart(context.ContentPartName))
        {
            SetMembership(subject: false, context.ContentTypeName, isMember: false);
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

    private void Apply(ContentTypeDefinition contentTypeDefinition)
    {
        if (contentTypeDefinition is null)
        {
            return;
        }

        SetMembership(subject: true, contentTypeDefinition.Name, OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentTypeDefinition));
        SetMembership(subject: false, contentTypeDefinition.Name, OmnichannelContactDefinitionService.HasOmnichannelContactPart(contentTypeDefinition));
    }

    private void SetMembership(bool subject, string contentType, bool isMember)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return;
        }

        lock (_lock)
        {
            // Skip incremental updates until the set has been warmed; the initial warm reads the current
            // definitions and therefore already reflects any change that happened before it ran.
            if (!_memoryCache.TryGetValue<OmnichannelContentTypeSet>(_cacheKey, out var current) || current is null)
            {
                _set = null;

                return;
            }

            var updated = current.With(subject, contentType, isMember);

            if (!ReferenceEquals(updated, current))
            {
                _memoryCache.Set(_cacheKey, updated);
            }

            _set = updated;
        }
    }

    private static bool IsSubjectPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelSubject, StringComparison.Ordinal);

    private static bool IsContactPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal);
}
