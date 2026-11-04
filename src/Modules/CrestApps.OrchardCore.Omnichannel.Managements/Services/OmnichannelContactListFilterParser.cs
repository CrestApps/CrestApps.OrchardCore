using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Parses the content list's search box, reading the free words of a search on a contact or lead list as one phrase.
/// </summary>
/// <remarks>
/// <para>
/// The search box joins its free words with "or", and each word is matched against the display text through its own
/// join of the content item index. An "or" across two joins cannot be answered from an index, so a search for a name
/// such as <c>John Smith</c> read every record of the list and outlived the command timeout on a large tenant. On a
/// contact or lead list the words are a name or a number, so they are searched as one phrase, which is one match on one
/// join, and a number written with spaces is compared with phone numbers as one number.
/// </para>
/// <para>
/// A search that uses the search syntax itself (quotes, parentheses, <c>AND</c>, <c>OR</c>, <c>NOT</c> or a leading
/// <c>-</c>) is parsed as typed, and named terms such as <c>converted:false</c> are kept as they are. Every other
/// content list is parsed as typed.
/// </para>
/// </remarks>
internal sealed class OmnichannelContactListFilterParser : IContentsAdminListFilterParser
{
    private static readonly HashSet<string> _operators = new(StringComparer.OrdinalIgnoreCase) { "AND", "OR", "NOT" };

    private readonly IQueryParser<ContentItem> _parser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactListFilterParser"/> class.
    /// </summary>
    /// <param name="parser">The parser built from every content list filter provider.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, which carries the list's content types.</param>
    /// <param name="contentTypeProvider">The provider that tells contact and lead types apart.</param>
    public OmnichannelContactListFilterParser(
        IQueryParser<ContentItem> parser,
        IHttpContextAccessor httpContextAccessor,
        OmnichannelContentTypeProvider contentTypeProvider)
    {
        _parser = parser;
        _httpContextAccessor = httpContextAccessor;
        _contentTypeProvider = contentTypeProvider;
    }

    /// <inheritdoc/>
    public QueryFilterResult<ContentItem> Parse(string text)
        => _parser.Parse(IsContactOnlyList() ? ToPhraseSearch(text) : text);

    /// <summary>
    /// Returns the search with its free words joined into one quoted phrase after its named terms, or the search as it
    /// was when it holds fewer than two free words or uses the search syntax itself.
    /// </summary>
    /// <param name="text">The text of the search box.</param>
    internal static string ToPhraseSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.AsSpan().IndexOfAny("\"()") >= 0)
        {
            return text;
        }

        var namedTerms = new List<string>();
        var words = new List<string>();

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (_operators.Contains(token) || token.StartsWith('-'))
            {
                return text;
            }

            if (token.Contains(':'))
            {
                namedTerms.Add(token);

                continue;
            }

            words.Add(token);
        }

        if (words.Count < 2)
        {
            return text;
        }

        namedTerms.Add($"\"{string.Join(' ', words)}\"");

        return string.Join(' ', namedTerms);
    }

    // The same test the phone-aware search uses, read from the snapshot because parsing is synchronous. The content
    // types are warmed by the admin menu before any list renders; until then the search is parsed as typed.
    private bool IsContactOnlyList()
    {
        if (_httpContextAccessor.HttpContext?.Request.RouteValues.TryGetValue("contentTypeId", out var routeValue) != true ||
            routeValue is not string contentTypeId)
        {
            return false;
        }

        var contentTypes = contentTypeId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return contentTypes.Length > 0 && contentTypes.All(_contentTypeProvider.IsContactContentType);
    }
}
