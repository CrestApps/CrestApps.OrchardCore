using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The rules a change reported by a list's provider is applied by.
/// </summary>
public static class MailingListProviderChanges
{
    /// <summary>
    /// Returns whether a reported change is older than the member's last status change and must be ignored, so a late
    /// webhook never subscribes someone again. A change at the same moment is applied.
    /// </summary>
    /// <param name="member">The member, or <see langword="null"/> when the address is not on the list.</param>
    /// <param name="change">The reported change.</param>
    /// <returns><see langword="true"/> when the change is stale.</returns>
    public static bool IsStale(MailingListMember member, MailingListProviderChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return member is not null && Utc(change.OccurredUtc) < Utc(member.StatusChangedUtc);
    }

    /// <summary>
    /// Returns the state a reported change moves the member to, or <see langword="null"/> when it changes no state or
    /// the move is not allowed (a cleaned address must confirm again on the site).
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="change">The reported change.</param>
    /// <returns>The new state, or <see langword="null"/>.</returns>
    public static MailingListMemberStatus? NextStatus(MailingListMember member, MailingListProviderChange change)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(change);

        if (change.Status is not { } status || status == member.Status)
        {
            return null;
        }

        return MailingListMemberTransitions.CanMove(member.Status, status) ? status : null;
    }

    /// <summary>
    /// Returns whether a reported change of the member runs silently, with no welcome e-mail and no welcome coupon from
    /// the site: the member never signed up on the site. The provider reported it (a sign-up through the provider's own
    /// form, or an address its admins added), now or in an earlier change, or an admin or an import added it. A member
    /// who signed up on the site and is then confirmed by the provider is not silent. A site sign-up of an address the
    /// provider reported, while it is not subscribed, makes it a site sign-up; a subscribed one keeps its source.
    /// </summary>
    /// <param name="member">The member, as the change leaves it.</param>
    /// <returns><see langword="true"/> when the change runs silently.</returns>
    public static bool IsSilent(MailingListMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return member.Source is MailingListMemberSources.Provider or MailingListMemberSources.Admin or MailingListMemberSources.Import;
    }

    private static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
