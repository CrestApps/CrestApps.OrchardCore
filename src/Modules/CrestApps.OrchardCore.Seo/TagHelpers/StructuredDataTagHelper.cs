using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Seo.TagHelpers;

/// <summary>
/// The <c>&lt;structured-data&gt;</c> tag helper: adds the JSON-LD it holds to the page's structured data and writes
/// nothing, so a Razor view can describe its page. Import it with <c>@addTagHelper *, CrestApps.OrchardCore.Seo</c>.
/// </summary>
/// <example>
/// <code>
/// &lt;structured-data&gt;{ "@type": "ContactPage", "name": "Contact us" }&lt;/structured-data&gt;
/// </code>
/// </example>
[HtmlTargetElement("structured-data")]
public sealed class StructuredDataTagHelper : TagHelper
{
    private readonly IPageStructuredData _pageStructuredData;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredDataTagHelper"/> class.
    /// </summary>
    /// <param name="pageStructuredData">The structured data of the current page.</param>
    /// <param name="logger">The logger.</param>
    public StructuredDataTagHelper(IPageStructuredData pageStructuredData, ILogger<StructuredDataTagHelper> logger)
    {
        _pageStructuredData = pageStructuredData;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var content = await output.GetChildContentAsync();

        // Values written with @ are HTML-encoded, so their quotes are decoded before the JSON is read.
        var json = WebUtility.HtmlDecode(content.GetContent());

        if (!string.IsNullOrWhiteSpace(json) && !_pageStructuredData.AddJsonLd(json))
        {
            _logger.LogWarning("The JSON-LD in a structured-data tag is not valid JSON and is ignored.");
        }

        output.SuppressOutput();
    }
}
