namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// The e-mail addresses that unsubscribed from survey invitations, kept as hashes of the trimmed lowercase address so
/// the list holds no readable address.
/// </summary>
public sealed class SurveyInvitationOptOutDocument
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the hashes of the addresses that unsubscribed.
    /// </summary>
    public HashSet<string> AddressHashes { get; set; } = new(StringComparer.Ordinal);
}
