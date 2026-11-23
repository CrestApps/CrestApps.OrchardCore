using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Names mailing list members (record kind <see cref="MailingListsConstants.MemberRecordKind"/>) for record links,
/// timelines and the source of a welcome coupon. Members are not searched here: they are found on their list's page.
/// </summary>
public sealed class MailingListMemberRecordSource : IRecordSource
{
    private readonly IMailingListMemberStore _members;
    private readonly IMailingListManager _lists;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberRecordSource"/> class.
    /// </summary>
    /// <param name="members">The member store.</param>
    /// <param name="lists">The list manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator, for the member's admin page.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public MailingListMemberRecordSource(
        IMailingListMemberStore members,
        IMailingListManager lists,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _members = members;
        _lists = lists;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Kind
        => MailingListsConstants.MemberRecordKind;

    /// <inheritdoc/>
    [JsonIgnore]
    public LocalizationSource DisplayName
        => LocalizationSource.Create<MailingListMemberRecordSource>("Mailing list member");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, MailingListsPermissions.ViewMailingListMembers);

    /// <inheritdoc/>
    public Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RecordSummary>>([]);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        var lists = new Dictionary<string, string>(StringComparer.Ordinal);
        var httpContext = _httpContextAccessor.HttpContext;

        foreach (var id in ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal) ?? [])
        {
            var member = await _members.FindByIdAsync(id, cancellationToken);

            if (member is null)
            {
                continue;
            }

            if (!lists.TryGetValue(member.ListId, out var listName))
            {
                listName = (await _lists.FindByIdAsync(member.ListId, cancellationToken))?.DisplayText;
                lists[member.ListId] = listName;
            }

            summaries[id] = new RecordSummary
            {
                Id = id,
                Title = member.Contact?.Email ?? member.NormalizedEmail,
                Description = listName,
                AdminUrl = httpContext is null
                    ? null
                    : _linkGenerator.GetPathByAction(httpContext, "Details", "MailingListMembers", new { area = MailingListsConstants.Features.MailingLists, listId = member.ListId, id }),
            };
        }

        return summaries;
    }
}
