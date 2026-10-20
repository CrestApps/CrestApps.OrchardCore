using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Contents;
using OrchardCore.Contents.ViewModels;
using YesSql;
using YesSql.Filters.Nodes;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Builds the count of a leads list from the lead index alone, when the list is filtered by nothing the lead index
/// cannot answer.
/// </summary>
/// <remarks>
/// <para>
/// The content list counts its items with the query that lists them, which joins the document table, the content
/// item index and the lead index and counts the distinct documents. On a large tenant that reads every lead row of
/// three tables on every page of the list, and it outlived the command timeout. Every lead version the list shows has
/// exactly one lead index row with the same version flags, so the same number is a count of that one table.
/// </para>
/// <para>
/// The count is only built when it is certain to be the number the list shows, and the list's own count runs
/// otherwise: the request names only lead types, the user may view the leads of others in every one of them (else the
/// list keeps only the user's own), the list is filtered by the latest, published or unpublished version, at least one
/// lead term is used (a lead with no lead index row is listed only without one), every term is a plain value, there is
/// no free-text search, and the request carries nothing but the search and the page.
/// </para>
/// </remarks>
internal sealed class LeadListCountQueryFactory
{
    private const string _statusTermName = "status";
    private const string _typeTermName = "type";
    private const string _sortTermName = "sort";

    private static readonly HashSet<string> _allowedQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "q",
        "page",
        "pagesize",
    };

    private readonly ISession _session;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly OmnichannelContentTypeProvider _contentTypeProvider;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadListCountQueryFactory"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="contentTypeProvider">The omnichannel content type provider.</param>
    /// <param name="serviceProvider">The services of the request, which resolve the term values.</param>
    public LeadListCountQueryFactory(
        ISession session,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        OmnichannelContentTypeProvider contentTypeProvider,
        IServiceProvider serviceProvider)
    {
        _session = session;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _contentTypeProvider = contentTypeProvider;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Builds the lead index query that counts the leads of the list, or <see langword="null"/> when only the list's
    /// own count is certain to be right.
    /// </summary>
    /// <param name="model">The options of the content list, with the filters it was given.</param>
    /// <returns>The count query, or <see langword="null"/>.</returns>
    public async ValueTask<IQueryIndex<LeadIndex>> TryCreateAsync(ContentOptionsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null ||
            model.FilterResult is null ||
            !HttpMethods.IsGet(httpContext.Request.Method) ||
            httpContext.Request.Query.Keys.Any(key => !_allowedQueryKeys.Contains(key)))
        {
            return null;
        }

        var contentTypes = await GetLeadContentTypesAsync(httpContext);

        if (contentTypes is null)
        {
            return null;
        }

        var status = ContentsStatus.Latest;
        var leadTerms = new List<(string Name, string Value)>();

        foreach (var term in model.FilterResult)
        {
            if (string.Equals(term.TermName, _typeTermName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(term.TermName, _sortTermName, StringComparison.OrdinalIgnoreCase))
            {
                // The type term carries the types of the route, read above; the order does not change a count.
                continue;
            }

            if (term is not NamedTermNode namedTerm ||
                namedTerm.Operation?.GetType() != typeof(UnaryNode))
            {
                // A free-text search, or a term with "not", "or" or "and" in its value.
                return null;
            }

            var value = ((UnaryNode)namedTerm.Operation).Value;

            if (string.Equals(term.TermName, _statusTermName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadStatus(value, out status))
                {
                    return null;
                }

                continue;
            }

            if (!LeadListTermPredicates.TermNames.Contains(term.TermName))
            {
                return null;
            }

            leadTerms.Add((term.TermName, value));
        }

        if (leadTerms.Count == 0)
        {
            return null;
        }

        var query = _session.QueryIndex<LeadIndex>(index => index.ContentType.IsIn(contentTypes));

        query = status switch
        {
            ContentsStatus.Published => query.Where(index => index.Published),
            ContentsStatus.Draft => query.Where(index => index.Latest && !index.Published),
            _ => query.Where(index => index.Latest),
        };

        foreach (var (name, value) in leadTerms)
        {
            query = query.Where(await LeadListTermPredicates.BuildAsync(name, value, _serviceProvider));
        }

        return query;
    }

    // The content list carries its types as a route value (.../ContentItems/{contentTypeId}), several separated by
    // commas. Every one of them must be a lead type the user may view in full, as the list's type term would then
    // filter by the types alone.
    private async ValueTask<string[]> GetLeadContentTypesAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.RouteValues.TryGetValue("contentTypeId", out var routeValue) ||
            routeValue is not string routeContentTypeId)
        {
            return null;
        }

        var contentTypes = routeContentTypeId
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (contentTypes.Length == 0)
        {
            return null;
        }

        var leadContentTypes = await _contentTypeProvider.GetLeadContentTypesAsync();

        foreach (var contentType in contentTypes)
        {
            if (!leadContentTypes.Contains(contentType))
            {
                return null;
            }

            // Passing no owner checks whether the user may view the leads of others, as the list's type term does.
            if (!await _authorizationService.AuthorizeContentTypeAsync(httpContext.User, CommonPermissions.ViewContent, contentType, owner: null))
            {
                return null;
            }
        }

        return contentTypes;
    }

    // The list's status term reads "latest" and "all versions" as the latest version, and an unknown value too.
    // "Owned by me" filters by the content owner, which the lead index does not hold.
    private static bool TryReadStatus(string value, out ContentsStatus status)
    {
        if (!Enum.TryParse(value, true, out status))
        {
            status = ContentsStatus.Latest;

            return true;
        }

        return status != ContentsStatus.Owner;
    }
}
