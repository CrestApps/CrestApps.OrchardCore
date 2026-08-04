using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reports the content types that have the <c>OmnichannelSubjectPart</c> or the <c>OmnichannelContactPart</c>
/// attached, so callers can answer that question and build subject and contact content type drop downs without
/// scanning every content type definition on each request. The membership sets are computed once and cached in
/// the per-tenant <see cref="IMemoryCache"/>. The cache entry is invalidated by
/// <c>OmnichannelContentTypeCacheInvalidator</c> when a content definition changes, so the next read recomputes
/// fresh sets that reflect the latest definitions.
/// </summary>
public sealed class OmnichannelContentTypeProvider
{
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

        _memoryCache.Set(_cacheKey, set);
        _set = set;

        return set;
    }
}
