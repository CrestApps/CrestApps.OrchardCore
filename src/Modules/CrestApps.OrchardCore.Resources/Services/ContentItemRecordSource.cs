using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Resources.Services;

/// <summary>
/// Makes content items a record kind (<c>Content</c>, the id is the content item id), so any module can link to an
/// account, a contact, a product or any other content item and show its title. A user finds only the items they may
/// view.
/// </summary>
internal sealed class ContentItemRecordSource : IRecordSource
{
    /// <summary>
    /// The record kind of content items.
    /// </summary>
    public const string RecordKind = RecordKinds.ContentItem;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ContentItemRecordSource(
        ISession session,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    public string Kind => RecordKind;

    [JsonIgnore]
    public LocalizationSource DisplayName => LocalizationSource.Create<ContentItemRecordSource>("Content item");

    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, CommonPermissions.ListContent);

    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        var text = search.Text?.Trim();
        var query = string.IsNullOrEmpty(text)
            ? _session.QueryIndex<ContentItemIndex>(index => index.Latest)
            // Names that start with the text: a prefix search uses the display text index, where "contains" reads every
            // content item of every type.
            : _session.QueryIndex<ContentItemIndex>(index => index.Latest && index.DisplayText.StartsWith(text));

        // A few more than asked, since the items the user may not view are left out.
        var rows = await query.OrderBy(index => index.DisplayText).Take(search.Count * 2).ListAsync(cancellationToken);
        var items = await _contentManager.GetAsync(rows.Select(row => row.ContentItemId).Distinct(), VersionOptions.Latest);
        var visible = new List<ContentItem>();

        foreach (var item in items)
        {
            if (visible.Count < search.Count && await _authorizationService.AuthorizeAsync(search.User, CommonPermissions.ViewContent, item))
            {
                visible.Add(item);
            }
        }

        return [.. visible.Select(ToSummary)];
    }

    public async Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        }

        var items = await _contentManager.GetAsync(wanted, VersionOptions.Latest);

        return items.ToDictionary(item => item.ContentItemId, ToSummary, StringComparer.Ordinal);
    }

    private RecordSummary ToSummary(ContentItem item)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new RecordSummary
        {
            Id = item.ContentItemId,
            Title = string.IsNullOrWhiteSpace(item.DisplayText) ? item.ContentItemId : item.DisplayText,
            Description = item.ContentType,
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, "Display", "Admin", new { area = "OrchardCore.Contents", contentItemId = item.ContentItemId }),
        };
    }
}
