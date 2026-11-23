using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Turns what Mailchimp reports (a webhook, or a member read by the reconciliation) into the change the member service
/// applies. The member's id at the provider is always the subscriber hash, so it matches what pushes return.
/// </summary>
public static class MailchimpChangeMapper
{
    /// <summary>
    /// Maps a webhook:
    /// <list type="bullet">
    /// <item><description><c>subscribe</c>: subscribed and verified (Mailchimp's double opt-in, or a sign-up through a
    /// Mailchimp form).</description></item>
    /// <item><description><c>unsubscribe</c>: unsubscribed, with Mailchimp's reason (<c>manual</c>, <c>abuse</c>) or
    /// <c>deleted</c> when an admin deleted the member.</description></item>
    /// <item><description><c>cleaned</c>: cleaned, with Mailchimp's reason (<c>hard</c>, <c>abuse</c>); no consent
    /// opt-out is recorded for it.</description></item>
    /// <item><description><c>profile</c>: the name only.</description></item>
    /// <item><description><c>upemail</c>: the new address.</description></item>
    /// </list>
    /// Other events, and events without an address, change nothing.
    /// </summary>
    /// <param name="list">The site's list the webhook was sent for.</param>
    /// <param name="webhook">The webhook.</param>
    /// <param name="receivedUtc">When the site received the webhook, used when it has no <c>fired_at</c>.</param>
    /// <returns>The change, or <see langword="null"/> when there is nothing to apply.</returns>
    public static MailingListProviderChange FromWebhook(MailingList list, MailchimpWebhookEvent webhook, DateTime receivedUtc)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(webhook);

        var occurredUtc = webhook.FiredUtc ?? AsUtc(receivedUtc);
        var name = ReadName(list, webhook.Merges);

        if (webhook.Type == MailchimpWebhookParser.AddressChange)
        {
            if (string.IsNullOrEmpty(webhook.OldEmail) || string.IsNullOrEmpty(webhook.NewEmail))
            {
                return null;
            }

            return new MailingListProviderChange
            {
                ListId = list.ItemId,
                Email = webhook.OldEmail,
                NewEmail = webhook.NewEmail,
                ProviderMemberId = MailchimpSubscriberHash.Compute(webhook.NewEmail),
                OccurredUtc = occurredUtc,
            };
        }

        if (string.IsNullOrEmpty(webhook.Email))
        {
            return null;
        }

        var providerMemberId = MailchimpSubscriberHash.Compute(webhook.Email);

        return webhook.Type switch
        {
            MailchimpWebhookParser.Subscribe => new MailingListProviderChange
            {
                ListId = list.ItemId,
                Email = webhook.Email,
                Status = MailingListMemberStatus.Subscribed,
                Verified = true,
                Name = name,
                ProviderMemberId = providerMemberId,
                OccurredUtc = occurredUtc,
            },
            MailchimpWebhookParser.Unsubscribe => new MailingListProviderChange
            {
                ListId = list.ItemId,
                Email = webhook.Email,
                Status = MailingListMemberStatus.Unsubscribed,
                Reason = webhook.Action == "delete" ? "deleted" : webhook.Reason ?? webhook.Action,
                ProviderMemberId = providerMemberId,
                OccurredUtc = occurredUtc,
            },
            MailchimpWebhookParser.Cleaned => new MailingListProviderChange
            {
                ListId = list.ItemId,
                Email = webhook.Email,
                Status = MailingListMemberStatus.Cleaned,
                Reason = webhook.Reason,
                ProviderMemberId = providerMemberId,
                OccurredUtc = occurredUtc,
            },
            MailchimpWebhookParser.Profile when name is not null => new MailingListProviderChange
            {
                ListId = list.ItemId,
                Email = webhook.Email,
                Name = name,
                ProviderMemberId = providerMemberId,
                OccurredUtc = occurredUtc,
            },
            _ => null,
        };
    }

    /// <summary>
    /// Maps a member the reconciliation read. A subscribed member counts as verified only on a list whose confirmation
    /// is the provider's (Mailchimp's double opt-in): the site's own pushes also make members subscribed there.
    /// </summary>
    /// <param name="list">The site's list.</param>
    /// <param name="member">The member Mailchimp returned.</param>
    /// <param name="fallbackUtc">The time used when Mailchimp gave no <c>last_changed</c>.</param>
    /// <returns>The change, or <see langword="null"/> when there is nothing to apply.</returns>
    public static MailingListProviderChange FromMember(MailingList list, MailchimpMember member, DateTime fallbackUtc)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        if (string.IsNullOrWhiteSpace(member.EmailAddress))
        {
            return null;
        }

        var status = MailchimpPushPlanner.ToMemberStatus(member.Status);
        var name = ReadName(list, member.MergeFields);

        if (status is null && name is null)
        {
            return null;
        }

        return new MailingListProviderChange
        {
            ListId = list.ItemId,
            Email = member.EmailAddress,
            Status = status,
            Verified = status == MailingListMemberStatus.Subscribed && list.Confirmation == MailingListConfirmation.Provider,
            Name = name,
            Reason = member.Status == MailchimpMemberStatuses.Archived ? MailchimpMemberStatuses.Archived : null,
            ProviderMemberId = MailchimpSubscriberHash.IsHash(member.Id) ? member.Id : MailchimpSubscriberHash.Compute(member.EmailAddress),
            OccurredUtc = member.LastChangedUtc ?? AsUtc(fallbackUtc),
        };
    }

    private static string ReadName(MailingList list, IReadOnlyDictionary<string, string> mergeFields)
    {
        if (mergeFields is null || !list.GetMailchimpSettings().SendNameMergeFields)
        {
            return null;
        }

        mergeFields.TryGetValue(MailchimpNames.FirstNameTag, out var firstName);
        mergeFields.TryGetValue(MailchimpNames.LastNameTag, out var lastName);

        return MailchimpNames.Join(firstName, lastName);
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
