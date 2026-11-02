using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Seo.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.Handlers;

/// <summary>
/// Clears the JSON-LD of Orchard Core's SEO aspect when this feature writes the page's JSON-LD, so the page does not
/// get the editor's JSON-LD twice. The editor's JSON-LD is not lost: <c>EditorJsonLdStructuredDataProvider</c>
/// merges it into the page's structured data.
/// </summary>
/// <remarks>
/// Orchard Core's SEO module fills <see cref="SeoAspect.GoogleSchema"/> in its part handler, which runs before this
/// handler because this feature's handlers are registered after Orchard Core's content handlers.
/// </remarks>
internal sealed class SeoAspectJsonLdHandler : ContentHandlerBase
{
    private readonly ISiteService _siteService;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeoAspectJsonLdHandler"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public SeoAspectJsonLdHandler(
        ISiteService siteService,
        IContentDefinitionManager contentDefinitionManager)
    {
        _siteService = siteService;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc/>
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.ForAsync<SeoAspect>(async aspect =>
        {
            if (string.IsNullOrEmpty(aspect.GoogleSchema))
            {
                return;
            }

            var settings = await _siteService.GetSettingsAsync<StructuredDataSettings>();

            if (!settings.RenderOnPages)
            {
                return;
            }

            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

            if (definition?.GetSettings<StructuredDataTypeSettings>()?.Disabled == true)
            {
                return;
            }

            aspect.GoogleSchema = null;
        });
    }
}
