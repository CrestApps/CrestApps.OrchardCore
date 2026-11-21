namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// A request to award a credential.
/// </summary>
public sealed class CredentialAwardRequest
{
    /// <summary>
    /// Gets or sets the content item id of the credential definition.
    /// </summary>
    public string DefinitionId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the recipient.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the kind of rule that awards it.
    /// </summary>
    public string RuleKind { get; set; }

    /// <summary>
    /// Gets or sets the id of what awards it. A manual award without one gets a new id, unless the person holds a valid
    /// manual award of the definition.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the user id of the staff member who awards it by hand.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets why it is awarded by hand.
    /// </summary>
    public string Reason { get; set; }
}
