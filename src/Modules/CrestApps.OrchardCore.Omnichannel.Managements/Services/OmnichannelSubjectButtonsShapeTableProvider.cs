using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Hides the default content editor action buttons (Publish, Save Draft, and Preview) for content items whose
/// content type has the <c>OmnichannelSubjectPart</c> attached. Subject content items are driven through the
/// omnichannel subject flow, so the standard authoring buttons Orchard Core injects are not wanted on their editor.
/// </summary>
internal sealed class OmnichannelSubjectButtonsShapeTableProvider : ShapeTableProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;


    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelSubjectButtonsShapeTableProvider"/> class.
    /// </summary>
    /// <param name="contentTypeProvider">The provider that reports whether a content type is an omnichannel subject.</param>
    public OmnichannelSubjectButtonsShapeTableProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public override async ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        var contentTypeProvider = _httpContextAccessor.HttpContext.RequestServices.GetService<OmnichannelContentTypeProvider>();

        if (contentTypeProvider is null)
        {
            return;
        }

        var subjectContentTypes = await contentTypeProvider.GetSubjectContentTypesAsync();

        builder.Describe("Content_UserTaskButton")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);

        builder.Describe("Content_PublishButton")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);

        builder.Describe("Content_UnpublishButton")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);

        builder.Describe("Content_SaveDraftButton")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);

        builder.Describe("Content_DeleteButton")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);

        builder.Describe("ContentPreview_Button")
            .Placement(context => IsSubjectEditor(context, subjectContentTypes), PlacementInfo.Hidden);
    }

    private static bool IsSubjectEditor(ShapePlacementContext context, IReadOnlyCollection<string> subjectContentTypes)
    {
        if (context.ZoneShape is not null &&
            context.ZoneShape.TryGetProperty<ContentItem>("ContentItem", out var contentItem) &&
            contentItem is not null)
        {
            return subjectContentTypes.Contains(contentItem.ContentType);
        }

        return false;
    }
}
