using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The moves a mailing list member may make between its states.
/// </summary>
public static class MailingListMemberTransitions
{
    private static readonly TransitionTable<MailingListMemberStatus> _table = new(new Dictionary<MailingListMemberStatus, MailingListMemberStatus[]>
    {
        [MailingListMemberStatus.Pending] = [MailingListMemberStatus.Subscribed, MailingListMemberStatus.Unsubscribed],
        [MailingListMemberStatus.Subscribed] = [MailingListMemberStatus.Unsubscribed, MailingListMemberStatus.Cleaned],
        [MailingListMemberStatus.Unsubscribed] = [MailingListMemberStatus.Pending, MailingListMemberStatus.Subscribed],
        [MailingListMemberStatus.Cleaned] = [MailingListMemberStatus.Pending],
    });

    /// <summary>
    /// Gets the table: Pending to Subscribed or Unsubscribed; Subscribed to Unsubscribed or Cleaned; Unsubscribed to
    /// Pending (a new sign-up with confirmation) or Subscribed (a list without confirmation); Cleaned to Pending only, so
    /// an address that bounced must confirm again.
    /// </summary>
    public static TransitionTable<MailingListMemberStatus> Table
        => _table;

    /// <summary>
    /// Returns whether a member may move from one state to another.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The new state.</param>
    /// <returns><see langword="true"/> when the move is allowed.</returns>
    public static bool CanMove(MailingListMemberStatus from, MailingListMemberStatus to)
        => Table.CanMove(from, to);
}
