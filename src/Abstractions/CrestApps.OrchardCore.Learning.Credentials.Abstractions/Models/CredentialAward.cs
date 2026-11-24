using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Learning.Credentials.Models;

/// <summary>
/// One digital credential awarded to a person: the definition and the achievement as they were, the recipient, the
/// dates, the revocation, the slot in the status list and the signed credential as it was issued.
/// </summary>
public sealed class CredentialAward : CatalogItem
{
    /// <summary>
    /// Gets or sets the unguessable code in the credential's public addresses (128 random bits).
    /// </summary>
    public string PublicId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the credential definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the content item version id of the definition when it was awarded, for reference only.
    /// </summary>
    public string DefinitionVersionId { get; set; }

    /// <summary>
    /// Gets or sets the achievement as it was awarded.
    /// </summary>
    public CredentialAchievement Achievement { get; set; }

    /// <summary>
    /// Gets or sets the user id of the recipient; empty after a personal data erasure.
    /// </summary>
    public string RecipientUserId { get; set; }

    /// <summary>
    /// Gets or sets how the credential names the recipient; empty after a personal data erasure.
    /// </summary>
    public CredentialRecipientIdentity Recipient { get; set; }

    /// <summary>
    /// Gets or sets the kind of rule that awarded it, such as <c>CourseCompleted</c> or <c>Manual</c>.
    /// </summary>
    public string RuleKind { get; set; }

    /// <summary>
    /// Gets or sets the id of what awarded it, such as the completed enrollment. With the definition, the rule kind and
    /// the recipient, it makes an award happen once.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the staff member who awarded it by hand.
    /// </summary>
    public string AwardedBy { get; set; }

    /// <summary>
    /// Gets or sets why it was awarded by hand.
    /// </summary>
    public string AwardReason { get; set; }

    /// <summary>
    /// Gets or sets when it was issued, in UTC.
    /// </summary>
    public DateTime IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it expires, in UTC, or <see langword="null"/> when it does not.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was revoked, in UTC.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user id of the person who revoked it.
    /// </summary>
    public string RevokedBy { get; set; }

    /// <summary>
    /// Gets or sets why it was revoked.
    /// </summary>
    public string RevokedReason { get; set; }

    /// <summary>
    /// Gets or sets whether the recipient chose to show their name on the public verification page.
    /// </summary>
    public bool ShowName { get; set; }

    /// <summary>
    /// Gets or sets the number of the status list that holds the award's revocation bit.
    /// </summary>
    public int StatusListNumber { get; set; }

    /// <summary>
    /// Gets or sets the award's index in its status list. The slot is never given to another award.
    /// </summary>
    public int StatusListIndex { get; set; }

    /// <summary>
    /// Gets or sets the Open Badges 3.0 credential as it was issued, in JSON-LD; empty after a personal data erasure.
    /// </summary>
    public string Credential { get; set; }

    /// <summary>
    /// Gets or sets the credential signed as a JSON Web Token, as it was issued; empty after a personal data erasure.
    /// </summary>
    public string Jwt { get; set; }

    /// <summary>
    /// Gets or sets when the recipient's personal data was erased from the award, in UTC.
    /// </summary>
    public DateTime? ErasedUtc { get; set; }

    /// <summary>
    /// Gets whether the award is revoked.
    /// </summary>
    public bool IsRevoked => RevokedUtc is not null;

    /// <summary>
    /// Tells the state of the award at a moment.
    /// </summary>
    /// <param name="utcNow">The moment, in UTC.</param>
    /// <returns>The state.</returns>
    public CredentialAwardStatus GetStatus(DateTime utcNow)
    {
        if (IsRevoked)
        {
            return CredentialAwardStatus.Revoked;
        }

        return ExpiresUtc is { } expires && expires <= utcNow
            ? CredentialAwardStatus.Expired
            : CredentialAwardStatus.Active;
    }
}
