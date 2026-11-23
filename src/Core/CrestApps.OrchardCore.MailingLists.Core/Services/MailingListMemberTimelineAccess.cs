using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Says who may read and write a member's timeline: the people who may see a list's members read it, and the people who
/// manage them add internal notes. The member never sees it.
/// </summary>
public sealed class MailingListMemberTimelineAccess : ITimelineAccessProvider
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IMailingListMemberStore _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberTimelineAccess"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="members">The member store.</param>
    public MailingListMemberTimelineAccess(
        IAuthorizationService authorizationService,
        IMailingListMemberStore members)
    {
        _authorizationService = authorizationService;
        _members = members;
    }

    /// <inheritdoc/>
    public string SubjectKind
        => MailingListsConstants.MemberRecordKind;

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef subject, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, MailingListsPermissions.ViewMailingListMembers);

    /// <inheritdoc/>
    public async Task<bool> CanAddAsync(ClaimsPrincipal user, RecordRef subject, string kind, TimelineVisibility visibility, CancellationToken cancellationToken = default)
        => user is not null
            && visibility == TimelineVisibility.Internal
            && string.Equals(kind, TimelineConstants.Kinds.Note, StringComparison.Ordinal)
            && await _authorizationService.AuthorizeAsync(user, MailingListsPermissions.ManageMailingListMembers)
            && !string.IsNullOrEmpty(subject.Id)
            && await _members.FindByIdAsync(subject.Id, cancellationToken) is not null;
}
