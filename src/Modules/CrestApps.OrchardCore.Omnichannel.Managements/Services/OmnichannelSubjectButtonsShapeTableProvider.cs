using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

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

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectButtonsShapeTableProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to warm the omnichannel content type cache.</param>
    /// <param name="contentTypeProvider">The provider that reports whether a content type is an omnichannel subject.</param>
    public OmnichannelSubjectButtonsShapeTableProvider(
        IContentDefinitionManager contentDefinitionManager,
        OmnichannelContentTypeProvider contentTypeProvider)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentTypeProvider = contentTypeProvider;
    }

    /// <inheritdoc/>
    public async ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        await _contentTypeProvider.EnsureInitializedAsync(_contentDefinitionManager);

        var contentTypeProvider = _contentTypeProvider;

        foreach (var shapeType in _shapeTypes)
        {
            builder.Describe(shapeType)
                .Placement(context => IsSubjectEditor(context, contentTypeProvider), _hidden);
        }
    }

    private static bool IsSubjectEditor(ShapePlacementContext context, OmnichannelContentTypeProvider contentTypeProvider)
    {
        if (context.ZoneShape is not null &&
            context.ZoneShape.TryGetProperty<ContentItem>("ContentItem", out var contentItem) &&
            contentItem is not null)
        {
            return contentTypeProvider.IsSubjectContentType(contentItem.ContentType);
        }

        return false;
    }
}
