using OrchardCore.ContentManagement;
using OrchardCore.Seo.Models;
using OrchardCore.Shortcodes.Services;
using Shortcodes;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Uses the SEO meta data an editor set with Orchard Core's SEO module: the meta description and the social image.
/// It is registered only when Orchard Core's SEO feature is enabled.
/// </summary>
internal sealed class SeoMetaStructuredDataProvider : IStructuredDataProvider
{
    private readonly IContentManager _contentManager;
    private readonly IShortcodeService _shortcodeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeoMetaStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="shortcodeService">The shortcode service, which processes the shortcodes the editor typed.</param>
    public SeoMetaStructuredDataProvider(
        IContentManager contentManager,
        IShortcodeService shortcodeService)
    {
        _contentManager = contentManager;
        _shortcodeService = shortcodeService;
    }

    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.SeoMeta;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.MainEntity is null || context.ContentItem?.TryGet<SeoMetaPart>(out _) != true)
        {
            return;
        }

        var aspect = await _contentManager.PopulateAspectAsync<SeoAspect>(context.ContentItem);
        var shortcodeContext = new Context
        {
            ["ContentItem"] = context.ContentItem,
        };

        if (!string.IsNullOrWhiteSpace(aspect.MetaDescription))
        {
            JsonLd.SetIfNotEmpty(context.MainEntity, "description", await _shortcodeService.ProcessAsync(aspect.MetaDescription, shortcodeContext));
        }

        var image = context.ToAbsoluteUrl(aspect.OpenGraphImage ?? aspect.TwitterImage);

        if (!string.IsNullOrEmpty(image))
        {
            context.MainEntity["image"] = image;
        }
    }
}
