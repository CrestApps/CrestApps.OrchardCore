using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Handles the unsubscribe links of one mailing list (scope kind <see cref="MailingListsConstants.UnsubscribeScopeKind"/>)
/// on the e-mail preferences page.
/// </summary>
public sealed class MailingListUnsubscribeScopeHandler : IUnsubscribeScopeHandler
{
    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberService _members;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListUnsubscribeScopeHandler"/> class.
    /// </summary>
    /// <param name="lists">The list manager.</param>
    /// <param name="members">The member service.</param>
    public MailingListUnsubscribeScopeHandler(
        IMailingListManager lists,
        IMailingListMemberService members)
    {
        _lists = lists;
        _members = members;
    }

    /// <inheritdoc/>
    public string Kind
        => MailingListsConstants.UnsubscribeScopeKind;

    /// <inheritdoc/>
    public async Task<string> GetTitleAsync(UnsubscribeScope scope, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scope?.Id))
        {
            return null;
        }

        return (await _lists.FindByIdAsync(scope.Id, cancellationToken))?.DisplayText;
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(UnsubscribeTicket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        if (string.IsNullOrEmpty(ticket.Scope?.Id) || string.IsNullOrWhiteSpace(ticket.Address))
        {
            return;
        }

        await _members.UnsubscribeAsync(ticket.Scope.Id, ticket.Address, MailingListMemberSources.UnsubscribeLink, reason: null, cancellationToken);
    }
}
