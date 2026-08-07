namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries the machine-readable reason a recording governance policy denied a request to start or resume recording.
/// </summary>
public sealed class RecordingDeniedEventData
{
    /// <summary>
    /// Gets or sets the stable machine-readable reason code describing why recording was denied.
    /// </summary>
    public string DenyReasonCode { get; set; }
}
