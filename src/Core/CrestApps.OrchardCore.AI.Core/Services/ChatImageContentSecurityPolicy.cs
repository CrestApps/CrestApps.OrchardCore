using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The image and media sources a page that renders AI chat may load, set under
/// <c>CrestApps_AI:Chat:ContentSecurityPolicy</c>.
/// </summary>
public sealed class ChatImageContentSecurityPolicyOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public const string SectionName = "CrestApps_AI:Chat:ContentSecurityPolicy";

    /// <summary>
    /// Gets or sets a value indicating whether pages that render chat send the policy. Defaults to <see langword="false"/>:
    /// the policy covers the whole page, so it also blocks what the site doesn't list, such as provider-hosted generated
    /// images, map tiles and external avatars on a page that carries a chat widget.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets the image and media sources allowed besides the site itself, <c>data:</c> and <c>blob:</c>, such as a map
    /// tile server or an AI provider's image host: <c>https://tile.openstreetmap.org</c>.
    /// </summary>
    public List<string> ImageSources { get; } = [];

    /// <summary>
    /// Gets or sets the media CDN address of the tenant (<c>OrchardCore_Media:CdnBaseUrl</c>). Its origin is allowed
    /// without being listed in <see cref="ImageSources"/>.
    /// </summary>
    public string MediaCdnBaseUrl { get; set; }
}

/// <summary>
/// Sends a <c>Content-Security-Policy</c> that limits where images, audio and video may load from on a page that renders
/// AI chat, when a site turns it on. The chat renders model output as Markdown, and DOMPurify keeps images, audio and
/// video, so a poisoned page or document could have the model write one whose URL carries the conversation, which the
/// browser would fetch without a click. The header is added beside any policy the site already sends; browsers enforce
/// every policy, so it only narrows images and media.
/// </summary>
/// <remarks>
/// It is a page-wide stopgap, off by default, until the chat renderer itself keeps only allowed sources (upstream, in
/// CrestApps.Core).
/// </remarks>
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

            // The tenant's media CDN serves its own media, so its origin is allowed without being listed.
            if (Uri.TryCreate(_options.MediaCdnBaseUrl?.Trim(), UriKind.Absolute, out var cdn) && cdn.Scheme is "https" or "http")
            {
                sources.Add(cdn.GetLeftPart(UriPartial.Authority));
            }

            var list = string.Join(' ', sources.Distinct(StringComparer.OrdinalIgnoreCase));

            // Audio and video load cross-origin without a click as well, and img-src does not cover them.
            return $"img-src {list}; media-src {list}";
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
