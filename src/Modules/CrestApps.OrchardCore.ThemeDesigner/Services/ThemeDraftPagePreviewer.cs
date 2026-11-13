using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Renders a page of this site by requesting it with the signed-in designer's own cookies, so the page is rendered
/// exactly as they see it: with their draft, their permissions and their theme.
/// </summary>
internal sealed partial class ThemeDraftPagePreviewer : IThemeDraftPagePreviewer
{
    /// <summary>
    /// The name of the HTTP client the previews are requested with.
    /// </summary>
    public const string HttpClientName = "ThemeDesignerPagePreview";

    // What the model reads of a page's body, so a long page can't fill its context.
    private const int MaxBodyLength = 20_000;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ThemeDesignDraftStore _draftStore;
    private readonly IMemoryCache _memoryCache;

    public ThemeDraftPagePreviewer(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ThemeDesignDraftStore draftStore,
        IMemoryCache memoryCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _draftStore = draftStore;
        _memoryCache = memoryCache;
    }

    public async Task<ThemeDraftPagePreview> RenderAsync(string path, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true || !IsLocalPath(path))
        {
            return null;
        }

        var request = httpContext.Request;
        var address = new Uri($"{request.Scheme}://{request.Host}{request.PathBase}{path}");

        using var message = new HttpRequestMessage(HttpMethod.Get, address);

        // The page is rendered as the designer: their cookies carry who they are, and so which draft they see.
        var cookies = request.Headers[HeaderNames.Cookie];

        if (cookies.Count > 0)
        {
            message.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookies.ToString());
        }

        message.Headers.TryAddWithoutValidation(HeaderNames.Accept, "text/html");

        // The page records which template rendered each of its parts under this id (see ThemeDesignShapeTraceEvents).
        var traceId = Guid.NewGuid().ToString("N");
        message.Headers.TryAddWithoutValidation(ThemeDesignShapeTraceEvents.TraceHeader, traceId);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var draft = await _draftStore.GetAsync(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));

        var preview = Parse(html);
        preview.Path = path;
        preview.StatusCode = (int)response.StatusCode;
        preview.RedirectedTo = response.Headers.Location?.ToString();
        preview.RenderedWithDraft = draft is { Preview: true } && draft.ChangeCount > 0;

        var traceKey = ThemeDesignShapeTraceEvents.GetCacheKey(traceId);

        if (_memoryCache.TryGetValue<ConcurrentQueue<ThemeDraftRenderedTemplate>>(traceKey, out var rendered))
        {
            preview.RenderedTemplates = rendered.ToArray();
            _memoryCache.Remove(traceKey);
        }

        preview.StylesheetSizes = await MeasureStylesheetsAsync(client, request, cookies.ToString(), preview.Stylesheets, cancellationToken);

        if (preview.StatusCode >= 500)
        {
            preview.Error = ExtractError(html) ?? response.ReasonPhrase;
        }

        return preview;
    }

    // The most stylesheets measured for one page.
    private const int MaxMeasuredStylesheets = 12;

    private static async Task<IReadOnlyList<ThemeDraftStylesheet>> MeasureStylesheetsAsync(
        HttpClient client,
        HttpRequest request,
        string cookies,
        IReadOnlyList<string> stylesheets,
        CancellationToken cancellationToken)
    {
        var sizes = new List<ThemeDraftStylesheet>();

        foreach (var url in stylesheets.Take(MaxMeasuredStylesheets))
        {
            var stylesheet = new ThemeDraftStylesheet
            {
                Url = url,
                IsSiteResource = url.Contains("/" + SiteResources.SiteResourcesConstants.RoutePrefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    url.Contains("/" + ThemeDesignerConstants.DraftResourcesRoutePrefix + "/", StringComparison.OrdinalIgnoreCase),
            };

            // Only this site's stylesheets are measured; a CDN's or another site's is never requested.
            if (IsLocalPath(url))
            {
                try
                {
                    var path = url.StartsWith(request.PathBase.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) && request.PathBase.HasValue
                        ? url[request.PathBase.Value.Length..]
                        : url;

                    using var message = new HttpRequestMessage(HttpMethod.Get, new Uri($"{request.Scheme}://{request.Host}{request.PathBase}{path}"));

                    if (!string.IsNullOrEmpty(cookies))
                    {
                        message.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookies);
                    }

                    using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        stylesheet.Bytes = (await response.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength;
                    }
                }
                catch (HttpRequestException)
                {
                    // The size is left out; the preview itself still stands.
                }
            }

            sizes.Add(stylesheet);
        }

        return sizes;
    }

    /// <summary>
    /// Gets whether <paramref name="path"/> is a path on this site, and not an address of another site.
    /// </summary>
    /// <param name="path">The path to check.</param>
    internal static bool IsLocalPath(string path)
        => !string.IsNullOrEmpty(path) &&
            path[0] == '/' &&
            (path.Length == 1 || (path[1] != '/' && path[1] != '\\')) &&
            !path.Contains("://", StringComparison.Ordinal);

    /// <summary>
    /// Reads the title, the stylesheets, the scripts and a compact copy of the body of a page.
    /// </summary>
    /// <param name="html">The page's HTML.</param>
    internal static ThemeDraftPagePreview Parse(string html)
    {
        html ??= string.Empty;

        var preview = new ThemeDraftPagePreview
        {
            Title = Text(TitlePattern().Match(html).Groups[1].Value),
            Stylesheets = StylesheetPattern().Matches(html)
                .Select(match => HrefPattern().Match(match.Value).Groups[1].Value)
                .Where(href => !string.IsNullOrEmpty(href))
                .Select(WebUtility.HtmlDecode)
                .ToArray(),
            Scripts = ScriptSourcePattern().Matches(html)
                .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
                .ToArray(),
        };

        var body = BodyPattern().Match(html);
        var markup = body.Success ? body.Groups[1].Value : html;

        markup = DraftBarPattern().Replace(markup, string.Empty);
        markup = ScriptBlockPattern().Replace(markup, string.Empty);
        markup = StyleBlockPattern().Replace(markup, string.Empty);
        markup = CommentPattern().Replace(markup, string.Empty);
        markup = SvgPattern().Replace(markup, "<svg/>");
        markup = WhiteSpacePattern().Replace(markup, " ").Trim();

        preview.Truncated = markup.Length > MaxBodyLength;
        preview.BodyMarkup = preview.Truncated ? markup[..MaxBodyLength] : markup;

        return preview;
    }

    // The developer exception page names the error in its title block; other error pages in their first heading.
    private static string ExtractError(string html)
    {
        var match = ErrorTitlePattern().Match(html);

        if (!match.Success)
        {
            match = HeadingPattern().Match(html);
        }

        return match.Success ? Text(match.Groups[1].Value) : null;
    }

    private static string Text(string html)
        => WebUtility.HtmlDecode(WhiteSpacePattern().Replace(TagPattern().Replace(html ?? string.Empty, " "), " ")).Trim();

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"<link\b[^>]*\brel\s*=\s*[""']?stylesheet[""']?[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex StylesheetPattern();

    [GeneratedRegex(@"\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefPattern();

    [GeneratedRegex(@"<script\b[^>]*\bsrc\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptSourcePattern();

    [GeneratedRegex(@"<body[^>]*>(.*)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BodyPattern();

    [GeneratedRegex(@"<div[^>]*data-theme-draft-bar.*?</form>\s*</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DraftBarPattern();

    [GeneratedRegex(@"<script\b.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptBlockPattern();

    [GeneratedRegex(@"<style\b.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleBlockPattern();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"<svg\b.*?</svg>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SvgPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpacePattern();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<div class=""titleerror"">(.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ErrorTitlePattern();

    [GeneratedRegex(@"<h[12][^>]*>(.*?)</h[12]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingPattern();
}
