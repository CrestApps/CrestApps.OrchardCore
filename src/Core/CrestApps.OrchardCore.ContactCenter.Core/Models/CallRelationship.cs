namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Links a call session to another session so a transfer chain can be walked in either direction without
/// reading provider metadata strings.
/// </summary>
public sealed class CallRelationship
{
    /// <summary>
    /// Gets or sets how the related session relates to this one.
    /// </summary>
    public CallRelationshipKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related call session.
    /// </summary>
    public string RelatedCallSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related interaction.
    /// </summary>
    public string RelatedInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier of the related session.
    /// </summary>
    public string RelatedProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the relationship was established.
    /// </summary>
    public DateTime EstablishedUtc { get; set; }
}
