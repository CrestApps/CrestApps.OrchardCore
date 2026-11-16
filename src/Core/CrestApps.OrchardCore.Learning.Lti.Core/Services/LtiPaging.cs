using Microsoft.AspNetCore.WebUtilities;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Pages the container responses of the services the way Names and Role Provisioning Services 2.0 (2.4.2) and Assignment
/// and Grade Services 2.0 (3.2.6) describe: a <c>limit</c> the tool asks for (within bounds) and an HTTP <c>Link</c> header
/// with the <c>next</c>, <c>prev</c>, <c>first</c> and <c>last</c> pages.
/// </summary>
public static class LtiPaging
{
    /// <summary>
    /// The page size when the tool asks for none.
    /// </summary>
    public const int DefaultLimit = 100;

    /// <summary>
    /// The largest page size.
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// Reads the page size and page number of a request.
    /// </summary>
    /// <param name="limit">The <c>limit</c> parameter.</param>
    /// <param name="page">The <c>page</c> parameter.</param>
    /// <returns>The page size, within bounds, and the page number, from 1.</returns>
    public static (int Limit, int Page) Read(string limit, string page)
    {
        var size = int.TryParse(limit, out var parsedLimit) && parsedLimit > 0 ? Math.Min(parsedLimit, MaxLimit) : DefaultLimit;
        var number = int.TryParse(page, out var parsedPage) && parsedPage > 0 ? parsedPage : 1;

        return (size, number);
    }

    /// <summary>
    /// Builds the <c>Link</c> header of a page.
    /// </summary>
    /// <param name="url">The URL of the request, with its other parameters.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="page">The page number, from 1.</param>
    /// <param name="total">The number of entries in all pages.</param>
    /// <returns>The header value, or an empty string when everything fits one page.</returns>
    public static string Link(string url, int limit, int page, int total)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        var last = Math.Max(1, (int)Math.Ceiling(total / (double)limit));

        if (last == 1 && page == 1)
        {
            return string.Empty;
        }

        var links = new List<string>();

        if (page < last)
        {
            links.Add($"<{PageUrl(url, limit, page + 1)}>; rel=\"next\"");
        }

        if (page > 1)
        {
            links.Add($"<{PageUrl(url, limit, Math.Min(page - 1, last))}>; rel=\"prev\"");
        }

        links.Add($"<{PageUrl(url, limit, 1)}>; rel=\"first\"");
        links.Add($"<{PageUrl(url, limit, last)}>; rel=\"last\"");

        return string.Join(", ", links);
    }

    private static string PageUrl(string url, int limit, int page)
    {
        var uri = new Uri(url, UriKind.Absolute);
        var query = QueryHelpers.ParseQuery(uri.Query)
            .Where(pair => pair.Key is not "page" and not "limit")
            .SelectMany(pair => pair.Value.Select(value => new KeyValuePair<string, string>(pair.Key, value)))
            .Append(new KeyValuePair<string, string>("limit", limit.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Append(new KeyValuePair<string, string>("page", page.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        return QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), query);
    }
}
