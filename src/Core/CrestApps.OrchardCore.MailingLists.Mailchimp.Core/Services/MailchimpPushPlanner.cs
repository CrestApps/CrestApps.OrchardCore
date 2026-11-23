using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Maps a member's state on the site to what a push sends to Mailchimp, and Mailchimp's statuses back to member states.
/// </summary>
public static class MailchimpPushPlanner
{
    /// <summary>
    /// Returns what a push of the member sends:
    /// <list type="bullet">
    /// <item><description>Erase: a permanent delete. Unsubscribe, or an upsert of an unsubscribed member: the status
    /// <c>unsubscribed</c> on an existing member only.</description></item>
    /// <item><description>A pending member, when the list's confirmation is the provider's: a new address is added as
    /// <c>pending</c> (Mailchimp sends its own confirmation e-mail). When the site sends the confirmation, nothing, so the
    /// person never gets two.</description></item>
    /// <item><description>A subscribed member: a new address is added as <c>subscribed</c>.</description></item>
    /// <item><description>A cleaned member: nothing; Mailchimp cleaned it, or will.</description></item>
    /// </list>
    /// An upsert never sets the status of an address Mailchimp already has; <see cref="FollowUpStatus"/> decides from the
    /// status Mailchimp returns whether a second call must change it. A retried push then finds the member as the first
    /// one left it and changes nothing, so Mailchimp never sends a second confirmation e-mail.
    /// </summary>
    /// <param name="list">The member's list.</param>
    /// <param name="member">The member as it is now on the site.</param>
    /// <param name="kind">What the push asks for.</param>
    /// <returns>The plan.</returns>
    public static MailchimpPushPlan Plan(MailingList list, MailingListMember member, MailingListPushKind kind)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        if (kind == MailingListPushKind.Erase)
        {
            return new MailchimpPushPlan { Action = MailchimpPushAction.Erase };
        }

        if (kind == MailingListPushKind.Unsubscribe)
        {
            return new MailchimpPushPlan { Action = MailchimpPushAction.Unsubscribe };
        }

        return member.Status switch
        {
            MailingListMemberStatus.Pending => list.Confirmation == MailingListConfirmation.Provider
                ? new MailchimpPushPlan
                {
                    Action = MailchimpPushAction.Upsert,
                    StatusIfNew = MailchimpMemberStatuses.Pending,
                }
                : MailchimpPushPlan.Skip,
            MailingListMemberStatus.Subscribed => new MailchimpPushPlan
            {
                Action = MailchimpPushAction.Upsert,
                StatusIfNew = MailchimpMemberStatuses.Subscribed,
            },
            MailingListMemberStatus.Unsubscribed => new MailchimpPushPlan { Action = MailchimpPushAction.Unsubscribe },
            _ => MailchimpPushPlan.Skip,
        };
    }

    /// <summary>
    /// Returns the status to give a member after an upsert that left its status at Mailchimp as it was. An address
    /// Mailchimp holds as unsubscribed, cleaned or archived gets <c>pending</c>, so Mailchimp asks the person to confirm
    /// before it mails them again; a transactional one gets the status a new address would get. A member Mailchimp holds
    /// as subscribed or pending is left as it is: a sign-up typed on the site never takes a confirmed subscriber back to
    /// pending, and a pending one is not sent a second confirmation e-mail.
    /// </summary>
    /// <param name="plan">The plan of the push.</param>
    /// <param name="mailchimpStatus">The member's status at Mailchimp after the upsert.</param>
    /// <returns>The status to set, or <see langword="null"/> when the member is right as it is.</returns>
    public static string FollowUpStatus(MailchimpPushPlan plan, string mailchimpStatus)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Action != MailchimpPushAction.Upsert)
        {
            return null;
        }

        return mailchimpStatus switch
        {
            MailchimpMemberStatuses.Unsubscribed or MailchimpMemberStatuses.Cleaned or MailchimpMemberStatuses.Archived => MailchimpMemberStatuses.Pending,
            MailchimpMemberStatuses.Transactional => plan.StatusIfNew,
            _ => null,
        };
    }

    /// <summary>
    /// Returns the member state of a Mailchimp status: <c>archived</c> counts as unsubscribed, and <c>transactional</c>
    /// or an unknown status has none.
    /// </summary>
    /// <param name="mailchimpStatus">The Mailchimp status.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public static MailingListMemberStatus? ToMemberStatus(string mailchimpStatus)
        => mailchimpStatus switch
        {
            MailchimpMemberStatuses.Subscribed => MailingListMemberStatus.Subscribed,
            MailchimpMemberStatuses.Unsubscribed or MailchimpMemberStatuses.Archived => MailingListMemberStatus.Unsubscribed,
            MailchimpMemberStatuses.Cleaned => MailingListMemberStatus.Cleaned,
            MailchimpMemberStatuses.Pending => MailingListMemberStatus.Pending,
            _ => null,
        };
}
