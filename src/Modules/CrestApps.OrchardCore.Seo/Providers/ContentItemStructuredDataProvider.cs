using System.Globalization;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Seo.Services;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Models;

namespace CrestApps.OrchardCore.Seo.Providers;

/// <summary>
/// Describes every content item with the data all content items have: its name, URL, dates and language, and a short
/// description taken from its body.
/// </summary>
internal sealed class ContentItemStructuredDataProvider : IStructuredDataProvider
{
    /// <summary>
    /// The longest description taken from the body of a content item.
    /// </summary>
    internal const int DescriptionLength = 300;

    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    public ContentItemStructuredDataProvider(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public int Order => StructuredDataProviderOrder.Defaults;

    /// <inheritdoc/>
    public async Task BuildAsync(StructuredDataContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contentItem = context.ContentItem;
        var entity = context.MainEntity;

        if (contentItem is null || entity is null)
        {
            return;
        }

        JsonLd.SetIfNotEmpty(entity, "name", contentItem.DisplayText);
        JsonLd.SetIfNotEmpty(entity, "url", context.Url);
        JsonLd.SetIfNotEmpty(entity, "dateCreated", FormatDate(contentItem.CreatedUtc));
        JsonLd.SetIfNotEmpty(entity, "datePublished", FormatDate(contentItem.PublishedUtc));
        JsonLd.SetIfNotEmpty(entity, "dateModified", FormatDate(contentItem.ModifiedUtc));

        var culture = await _contentManager.PopulateAspectAsync<CultureAspect>(contentItem);

        if (culture.HasCulture && !string.IsNullOrEmpty(culture.Culture?.Name))
        {
            entity["inLanguage"] = culture.Culture.Name;
        }

        var body = await _contentManager.PopulateAspectAsync<BodyAspect>(contentItem);

        if (body.Body is not null)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            body.Body.WriteTo(writer, HtmlEncoder.Default);

            var description = StructuredDataText.Truncate(StructuredDataText.ToPlainText(writer.ToString()), DescriptionLength);

            JsonLd.SetIfNotEmpty(entity, "description", description);
        }
    }

    /// <summary>
    /// Formats a UTC date as an ISO 8601 date and time, as schema.org dates expect.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The formatted date, or <see langword="null"/> when there is no date.</returns>
    internal static string FormatDate(DateTime? date)
    {
        if (date is null)
        {
            return null;
        }

        var utc = DateTime.SpecifyKind(date.Value, DateTimeKind.Utc);

        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
