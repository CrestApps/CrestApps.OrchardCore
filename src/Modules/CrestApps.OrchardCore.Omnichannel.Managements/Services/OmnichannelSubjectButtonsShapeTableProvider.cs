using Microsoft.Extensions.Caching.Memory;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Hides the default content editor action buttons (Publish, Save Draft, and Preview) for content items whose
/// content type has the <c>OmnichannelSubjectPart</c> attached. Subject content items are driven through the
/// omnichannel subject flow, so the standard authoring buttons Orchard Core injects are not wanted on their editor.
/// </summary>
internal sealed class OmnichannelSubjectButtonsShapeTableProvider : IShapeTableProvider
{
    private static readonly PlacementInfo _hidden = PlacementInfo.FromLocation(PlacementInfo.HiddenLocation);

    private static readonly string[] _shapeTypes =
    [
        "Content_PublishButton",
        "Content_SaveDraftButton",
        "ContentPreview_Button",
    ];

    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly IMemoryCache _memoryCache;
    private readonly string _cacheKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectButtonsShapeTableProvider"/> class.
    /// </summary>
    /// <param name="contentTypeProvider">The provider that reports whether a content type is an omnichannel subject.</param>
    /// <param name="memoryCache">The per-tenant memory cache the placement delegate reads the subject content types from.</param>
    /// <param name="shellSettings">The shell settings used to scope the cache entry to the current tenant.</param>
    public OmnichannelSubjectButtonsShapeTableProvider(
        OmnichannelContentTypeProvider contentTypeProvider,
        IMemoryCache memoryCache,
        ShellSettings shellSettings)
    {
        _contentTypeProvider = contentTypeProvider;
        _memoryCache = memoryCache;
        _cacheKey = OmnichannelContentTypeProvider.GetCacheKey(shellSettings);
    }

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        // Warm the per-tenant cache so the synchronous placement delegate below can read the current subject
        // content types. The shape table is cached for the tenant's lifetime, so the delegate reads the live
        // cache entry - kept current through the content definition notifications - rather than a snapshot.
        await _contentTypeProvider.GetSubjectContentTypesAsync();

        var memoryCache = _memoryCache;
        var cacheKey = _cacheKey;

        foreach (var shapeType in _shapeTypes)
        {
            builder.Describe(shapeType)
                .Placement(context => IsSubjectEditor(context, memoryCache, cacheKey), _hidden);
        }
    }

    private static bool IsSubjectEditor(ShapePlacementContext context, IMemoryCache memoryCache, string cacheKey)
    {
        if (context.ZoneShape is not null &&
            context.ZoneShape.TryGetProperty<ContentItem>("ContentItem", out var contentItem) &&
            contentItem is not null &&
            memoryCache.TryGetValue<OmnichannelContentTypeSet>(cacheKey, out var set) &&
            set is not null)
        {
            return set.SubjectContentTypes.Contains(contentItem.ContentType);
        }

        return false;
    }
}
