namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the machine-readable reason a recording governance policy denied a right-to-erasure request.
/// </summary>
public sealed class RecordingErasureDeniedEventData
{
    /// <summary>
    /// Gets or sets the identifier of the actor that requested erasure.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the stable machine-readable reason code describing why erasure was denied.
    /// </summary>
    public string DenyReasonCode { get; set; }
}
