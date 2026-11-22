namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// The e-mail addresses that unsubscribed from survey invitations, kept as keyed hashes (HMAC-SHA256) of the trimmed
/// lowercase address, so the list holds no readable address and can't be matched against a list of known addresses
/// without the key. The key is kept as ASP.NET Data Protection ciphertext, whose keys live outside the database.
/// Addresses added before the key existed are kept as plain SHA-256 hashes until they are changed again.
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

    /// <summary>
    /// Gets or sets the HMAC key of the address hashes, protected with ASP.NET Data Protection, or null before one was
    /// made.
    /// </summary>
    public string ProtectedKey { get; set; }
}
