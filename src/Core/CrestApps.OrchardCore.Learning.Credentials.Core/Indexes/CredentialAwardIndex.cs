using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Indexes;

/// <summary>
/// The index of credential awards.
/// </summary>
public sealed class CredentialAwardIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the public code.
    /// </summary>
    public string PublicId { get; set; }

    /// <summary>
    /// Gets or sets the content item id of the definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the recipient.
    /// </summary>
    public string RecipientUserId { get; set; }

    /// <summary>
    /// Gets or sets the rule kind.
    /// </summary>
    public string RuleKind { get; set; }

    /// <summary>
    /// Gets or sets the source id.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets when it was issued, in UTC.
    /// </summary>
    public DateTime IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it expires, in UTC.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets whether it is revoked.
    /// </summary>
    public bool Revoked { get; set; }

    /// <summary>
    /// Gets or sets the number of its status list.
    /// </summary>
    public int StatusListNumber { get; set; }

    /// <summary>
    /// Gets or sets its index in the status list.
    /// </summary>
    public int StatusListIndex { get; set; }
}
