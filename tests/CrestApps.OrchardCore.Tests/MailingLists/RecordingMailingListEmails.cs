using CrestApps.OrchardCore.MailingLists.Core.Emails;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// Records the mailing lists e-mails instead of sending them, with the nonce each link e-mail was made with.
/// </summary>
internal sealed class RecordingMailingListEmails : IMailingListEmails
{
    public List<(string Kind, string MemberId, string Nonce, MailingListIssuedCoupon Coupon)> Sent { get; } = [];

    public Task<bool> SendConfirmAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default)
        => Record("confirm", member, coupon);

    public Task<bool> SendWelcomeAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default)
        => Record("welcome", member, coupon);

    public Task<bool> SendClaimAsync(MailingList list, MailingListMember member, CancellationToken cancellationToken = default)
        => Record("claim", member, coupon: null);

    public Task<int> ForgetAsync(MailingListMember member, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    private Task<bool> Record(string kind, MailingListMember member, MailingListIssuedCoupon coupon)
    {
        Sent.Add((kind, member.ItemId, member.VerificationNonce, coupon));

        return Task.FromResult(true);
    }
}
