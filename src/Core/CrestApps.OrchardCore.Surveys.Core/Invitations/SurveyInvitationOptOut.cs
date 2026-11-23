namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// One e-mail address that unsubscribed from survey invitations, kept only as the keyed hash (HMAC-SHA256, prefixed
/// <c>h1:</c>) of its trimmed lowercase address, or as its plain SHA-256 hash when the list's key can't be read. Each
/// opt-out is its own record, found by its hash through <c>SurveyInvitationOptOutIndex</c>, so a check reads at most the
/// records of one address and two unsubscribes never touch the same record.
/// </summary>
public sealed class SurveyInvitationOptOut
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the hash of the address.
    /// </summary>
    public string AddressHash { get; set; }
}
