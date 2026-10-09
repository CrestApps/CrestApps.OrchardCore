namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Carries how and with what words a caller was told the call is recorded, so the notice can be proven later.
/// </summary>
public sealed class RecordingDisclosedEventData
{
    /// <summary>
    /// Gets or sets how the disclosure was given: a <see cref="ContactCenterConstants.RecordingDisclosureMethod"/> value.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// Gets or sets the disclosure text as it was configured when it was given.
    /// </summary>
    public string Text { get; set; }
}
