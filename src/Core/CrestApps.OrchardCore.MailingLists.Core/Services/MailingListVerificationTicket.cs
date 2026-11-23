namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// What a verification link's token holds: the member, the nonce the link was made with and when the link stops
/// working.
/// </summary>
/// <param name="MemberId">The member's id.</param>
/// <param name="Nonce">The member's verification nonce when the link was made; a newer link changes it.</param>
/// <param name="ExpiresUtc">When the link stops working (UTC), from the list's link lifetime.</param>
public sealed record MailingListVerificationTicket(string MemberId, string Nonce, DateTime ExpiresUtc);
