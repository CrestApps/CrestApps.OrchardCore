using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Seo.Drivers;

/// <summary>
/// Records the content item a page displays, so the page's JSON-LD describes it. It follows the rule of Orchard Core's
/// SEO module: the first content item displayed in the <c>Detail</c> display type, which is not a widget.
/// </summary>
internal sealed class StructuredDataContentDriver : ContentDisplayDriver
{
    private readonly IPageStructuredData _pageStructuredData;

    private bool _primaryContentRendered;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataContentDriver"/> class.
    /// </summary>
    /// <param name="pageStructuredData">The structured data of the current page.</param>
    public StructuredDataContentDriver(IPageStructuredData pageStructuredData)
    {
        _pageStructuredData = pageStructuredData;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (_primaryContentRendered)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        _primaryContentRendered = true;

        if (context.DisplayType == OrchardCoreConstants.DisplayType.Detail &&
            !context.Shape.TryGetProperty(nameof(ContentTypeSettings.Stereotype), out string _))
        {
            _pageStructuredData.ContentItem ??= contentItem;
        }

        return Task.FromResult<IDisplayResult>(null);
    }
}
