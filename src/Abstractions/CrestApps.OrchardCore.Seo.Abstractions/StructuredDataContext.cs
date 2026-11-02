using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The state shared by the <see cref="IStructuredDataProvider"/> instances while the structured data of a page or
/// of a content item is built.
/// </summary>
public sealed class StructuredDataContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataContext"/> class.
    /// </summary>
    /// <param name="contentItem">The content item, or <see langword="null"/> for a page without one.</param>
    /// <param name="purpose">What the structured data is built for.</param>
    /// <param name="baseUrl">The absolute URL of the site, without a trailing slash.</param>
    public StructuredDataContext(
        ContentItem contentItem,
        StructuredDataPurpose purpose,
        string baseUrl)
    {
        ContentItem = contentItem;
        Purpose = purpose;
        BaseUrl = baseUrl?.TrimEnd('/') ?? string.Empty;
    }

    /// <summary>
    /// Gets the content item being described, or <see langword="null"/> for a page that has none, such as a list or
    /// a custom page.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Gets what the structured data is built for.
    /// </summary>
    public StructuredDataPurpose Purpose { get; }

    /// <summary>
    /// Gets the absolute URL of the site, without a trailing slash, such as <c>https://example.com/tenant</c>.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets or sets the absolute URL of <see cref="ContentItem"/>, when it has one.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the node that describes <see cref="ContentItem"/>. Providers add properties to it. It is
    /// <see langword="null"/> when there is no content item or when its content type publishes no structured data.
    /// </summary>
    public JsonObject MainEntity { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type the content type settings give the content item, if any. Providers should not
    /// replace the <c>@type</c> of <see cref="MainEntity"/> when this is set.
    /// </summary>
    public string ConfiguredType { get; set; }

    /// <summary>
    /// Gets the other nodes of the page, such as the organization and the web site. Only used when
    /// <see cref="Purpose"/> is <see cref="StructuredDataPurpose.Page"/>.
    /// </summary>
    public JsonLdGraph Graph { get; } = new();

    /// <summary>
    /// Gets a bag that providers can use to share values while the structured data is built.
    /// </summary>
    public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Sets the <c>@type</c> of <see cref="MainEntity"/> unless the content type settings already chose one.
    /// </summary>
    /// <param name="type">The schema.org type.</param>
    public void SuggestType(string type)
    {
        if (MainEntity is null || string.IsNullOrEmpty(type) || !string.IsNullOrEmpty(ConfiguredType))
        {
            return;
        }

        MainEntity[StructuredDataConstants.Keywords.Type] = type;
    }

    /// <summary>
    /// Turns a site-relative URL, such as <c>/blog/post</c> or <c>~/media/image.jpg</c>, into an absolute URL.
    /// Absolute URLs are returned unchanged.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>The absolute URL, or <see langword="null"/> when <paramref name="url"/> is empty.</returns>
    public string ToAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return url;
        }

        if (string.IsNullOrEmpty(BaseUrl))
        {
            return url;
        }

        if (url.StartsWith("~/", StringComparison.Ordinal))
        {
            return $"{BaseUrl}/{url.Substring(2)}";
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return $"https:{url}";
        }

        if (url.StartsWith('/'))
        {
            // A root-relative URL already holds the tenant prefix, so it is resolved against the host only.
            var origin = Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri)
                ? baseUri.GetLeftPart(UriPartial.Authority)
                : BaseUrl;

            return $"{origin}{url}";
        }

        return $"{BaseUrl}/{url}";
    }
}
