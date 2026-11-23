using System.Globalization;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Exports a person's mailing list memberships with the evidence of their consent, and erases them through
/// <see cref="IMailingListMemberService.EraseAsync"/>, which also asks each list's provider to erase the address.
/// </summary>
public sealed class MailingListPersonalDataContributor : IPersonalDataContributor
{
    private readonly IMailingListMemberStore _members;
    private readonly IMailingListManager _lists;
    private readonly IMailingListMemberService _memberService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="members">The member store.</param>
    /// <param name="lists">The list manager.</param>
    /// <param name="memberService">The member service.</param>
    public MailingListPersonalDataContributor(
        IMailingListMemberStore members,
        IMailingListManager lists,
        IMailingListMemberService memberService)
    {
        _members = members;
        _lists = lists;
        _memberService = memberService;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lists = new Dictionary<string, MailingList>(StringComparer.Ordinal);

        foreach (var member in await FindAsync(context.Subject, cancellationToken))
        {
            if (!lists.TryGetValue(member.ListId, out var list))
            {
                list = await _lists.FindByIdAsync(member.ListId, cancellationToken);
                lists[member.ListId] = list;
            }

            var record = context.Add(MailingListsConstants.MemberRecordKind, member.ItemId, list?.DisplayText ?? member.ListId, member.CreatedUtc);

            record.Fields["Email"] = member.Contact?.Email ?? member.NormalizedEmail;
            record.Fields["Name"] = member.Contact?.Name;
            record.Fields["Status"] = member.Status.ToString();
            record.Fields["Source"] = member.Source;
            record.Fields["SubscribedUtc"] = Format(member.SubscribedUtc);
            record.Fields["UnsubscribedUtc"] = Format(member.UnsubscribedUtc);
            record.Fields["EmailVerifiedUtc"] = Format(member.EmailVerifiedUtc);
            record.Fields["ConsentText"] = member.Consent?.Text;
            record.Fields["ConsentForm"] = member.Consent?.Form;
            record.Fields["ConsentAgreedUtc"] = Format(member.Consent?.AgreedUtc);
            record.Fields["ConsentClientAddress"] = member.Consent?.ClientAddress;
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var addresses = (await FindAsync(context.Subject, cancellationToken))
            .Select(member => member.NormalizedEmail)
            .Where(address => !string.IsNullOrEmpty(address))
            .Distinct(StringComparer.Ordinal);

        foreach (var address in addresses)
        {
            context.Result.Count(MailingListsConstants.MemberRecordKind, await _memberService.EraseAsync(address, cancellationToken));
        }
    }

    private async Task<IReadOnlyList<MailingListMember>> FindAsync(PersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, MailingListMember>(StringComparer.Ordinal);

        foreach (var email in subject.Emails)
        {
            foreach (var member in await _members.ListByEmailAsync(EmailAddresses.Normalize(email), cancellationToken))
            {
                found.TryAdd(member.ItemId, member);
            }
        }

        foreach (var userId in subject.UserIds)
        {
            foreach (var member in await _members.ListByUserAsync(userId, cancellationToken))
            {
                found.TryAdd(member.ItemId, member);
            }
        }

        return [.. found.Values];
    }

    private static string Format(DateTime? utc)
        => utc is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture) : null;
}
