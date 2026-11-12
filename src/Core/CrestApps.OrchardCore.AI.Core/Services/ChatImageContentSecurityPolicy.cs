using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The image sources a page that renders AI chat may load, set under <c>CrestApps_AI:Chat:ContentSecurityPolicy</c>.
/// </summary>
public sealed class ChatImageContentSecurityPolicyOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "CrestApps_AI:Chat:ContentSecurityPolicy";

    /// <summary>
    /// Gets or sets a value indicating whether pages that render chat send the policy. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets the image sources allowed besides the site itself, <c>data:</c> and <c>blob:</c>, such as a media CDN:
    /// <c>https://cdn.example.com</c>.
    /// </summary>
    public List<string> ImageSources { get; } = [];
}

/// <summary>
/// Sends a <c>Content-Security-Policy</c> that limits where images may load from on a page that renders AI chat. The
/// chat renders model output as Markdown, and DOMPurify keeps images, so a poisoned page or document could have the
/// model write an image whose URL carries the conversation, which the browser would fetch without a click. The header is
/// added beside any policy the site already sends; browsers enforce every policy, so it only narrows images.
/// </summary>
public sealed class ChatImageContentSecurityPolicy
{
    private readonly ChatImageContentSecurityPolicyOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatImageContentSecurityPolicy"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    public ChatImageContentSecurityPolicy(IOptions<ChatImageContentSecurityPolicyOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Gets the policy the header carries.
    /// </summary>
    public string Policy
    {
        get
        {
            var sources = new List<string> { "'self'", "data:", "blob:" };

            foreach (var source in _options.ImageSources)
            {
                // A source holds no separator, so configuration can't add another directive.
                if (!string.IsNullOrWhiteSpace(source) && source.IndexOfAny([';', ',', '\'', '"', '\r', '\n']) < 0)
                {
                    sources.Add(source.Trim());
                }
            }

            return "img-src " + string.Join(' ', sources.Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Adds the policy to the response, once, unless it has started or the policy is turned off.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public void Apply(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        const string appliedKey = "CrestApps.AI.ChatImageContentSecurityPolicy";

        if (!_options.Enabled || httpContext.Response.HasStarted || httpContext.Items.ContainsKey(appliedKey))
        {
            return;
        }

        httpContext.Items[appliedKey] = true;
        httpContext.Response.Headers.Append("Content-Security-Policy", Policy);
    }
}
