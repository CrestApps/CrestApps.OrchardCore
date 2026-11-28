namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// The key of the survey invitation opt-outs: the HMAC key their address hashes are made with, kept as ASP.NET Data
/// Protection ciphertext, whose keys live outside the database, so the hashes can't be matched against a list of known
/// addresses without it. Each opt-out is its own record (<c>SurveyInvitationOptOut</c>). <see cref="AddressHashes"/>
/// is the old single list, which held every opt-out; the <c>Surveys.InvitationOptOutMove</c> resumable step moves its
/// hashes into records and empties it, and until then the opt-out store still honors them.
/// </summary>
public sealed class SurveyInvitationOptOutDocument
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the hashes of the old single list not moved into records yet; nothing adds to it any more.
    /// </summary>
    public HashSet<string> AddressHashes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the HMAC key of the address hashes, protected with ASP.NET Data Protection, or null before one was
    /// made.
    /// </summary>
    public string ProtectedKey { get; set; }
}
