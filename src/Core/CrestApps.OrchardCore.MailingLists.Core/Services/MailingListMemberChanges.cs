using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// The changes the member service makes to a member, kept apart so each rule reads on its own.
/// </summary>
internal static class MailingListMemberChanges
{
    internal static string LockKey(string listId, string normalizedEmail)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail ?? string.Empty));

        return $"MailingLists:Member:{listId}:{Convert.ToHexString(hash, 0, 12)}";
    }

    internal static string NewNonce()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    internal static void ApplySignUp(MailingListMember member, MailingList list, MailingListSubscribeRequest request, DateTime now)
    {
        var contact = member.Contact ?? new ContactInfo();

        member.Contact = contact with
        {
            Email = request.Email.Trim(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? contact.Name : request.Name.Trim(),
        };

        member.Source = string.IsNullOrWhiteSpace(request.Source) ? member.Source : request.Source;
        member.SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? member.SourceId : request.SourceId;
        member.UserId = string.IsNullOrEmpty(request.UserId) ? member.UserId : request.UserId;
        member.Consent = new MailingListConsentEvidence
        {
            Text = list.ConsentText,
            Form = string.IsNullOrWhiteSpace(request.ConsentForm) ? request.SourceId : request.ConsentForm,
            AgreedUtc = now,
            ClientAddress = request.ClientAddress,
        };
    }

    internal static bool ApplyProfile(MailingListMember member, MailingListProviderChange change)
    {
        var changed = false;

        member.Contact ??= new ContactInfo();

        if (!string.IsNullOrWhiteSpace(change.Name) && !string.Equals(member.Contact.Name, change.Name.Trim(), StringComparison.Ordinal))
        {
            member.Contact = member.Contact with { Name = change.Name.Trim() };
            changed = true;
        }

        if (!string.IsNullOrEmpty(change.ProviderMemberId) && !string.Equals(member.ProviderMemberId, change.ProviderMemberId, StringComparison.Ordinal))
        {
            member.ProviderMemberId = change.ProviderMemberId;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(change.Reason) && change.Status is MailingListMemberStatus.Unsubscribed or MailingListMemberStatus.Cleaned)
        {
            member.UnsubscribeReason = change.Reason.Trim();
        }

        return changed;
    }

    internal static void Move(MailingListMember member, MailingListMemberStatus status, DateTime whenUtc)
    {
        member.Status = status;
        member.StatusChangedUtc = whenUtc;

        switch (status)
        {
            case MailingListMemberStatus.Subscribed:
                member.SubscribedUtc = whenUtc;
                member.UnsubscribeReason = null;
                break;

            case MailingListMemberStatus.Unsubscribed:
            case MailingListMemberStatus.Cleaned:
                member.UnsubscribedUtc = whenUtc;

                // Links sent before the member left never bring them back.
                member.VerificationNonce = null;
                break;
        }
    }

    internal static MailingListMemberContext Context(MailingListMember member, MailingList list, MailingListMemberStatus? previous, string source, bool silent)
        => new()
        {
            Member = member,
            List = list,
            PreviousStatus = previous,
            Source = source,
            Silent = silent,
        };

    internal static bool IsActive(MailingListMember member)
        => member?.Status is MailingListMemberStatus.Pending or MailingListMemberStatus.Subscribed;

    internal static string SourceTag(string source)
        => source switch
        {
            MailingListMemberSources.Widget or MailingListMemberSources.Admin or MailingListMemberSources.Import or MailingListMemberSources.Api
                or MailingListMemberSources.Workflow or MailingListMemberSources.Provider or MailingListMemberSources.UnsubscribeLink
                or MailingListMemberSources.Consent or MailingListMemberSources.Retention => source,
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
}
