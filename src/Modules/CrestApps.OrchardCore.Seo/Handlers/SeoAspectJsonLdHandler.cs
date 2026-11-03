using CrestApps.OrchardCore.Seo.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SeoAspectJsonLdHandler"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public SeoAspectJsonLdHandler(ISiteService siteService)
    {
        _siteService = siteService;
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

            // An item whose structured data is turned off keeps the JSON-LD its editor typed, as Orchard Core writes it.
            if (!settings.RenderOnPages || (context.ContentItem.TryGet<SchemaOrgPart>(out var schemaOrgPart) && schemaOrgPart.Disabled))
            {
                return;
            }

            aspect.GoogleSchema = null;
        });
    }
}
