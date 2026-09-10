namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of a recording state-change command. Recording is a release-critical mutation, so
/// an interrupted or timed-out command reports an explicit indeterminate outcome rather than silently
/// collapsing to success or failure.
/// </summary>
public sealed class RecordingCommandResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the recording state change was applied and recorded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider may have executed the recording state change but
    /// its outcome could not be observed.
    /// </summary>
    public bool OutcomeUnknown { get; set; }

    /// <summary>
    /// Gets or sets an explanation of the outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="reason">The optional explanation.</param>
    /// <returns>A successful <see cref="RecordingCommandResult"/>.</returns>
    public static RecordingCommandResult Success(string reason = null)
    {
        return new RecordingCommandResult { Succeeded = true, Reason = reason };
    }

    /// <summary>
    /// Creates a failed result for a recording state change that was definitely not applied.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>A failed <see cref="RecordingCommandResult"/>.</returns>
    public static RecordingCommandResult Failure(string reason)
    {
        return new RecordingCommandResult { Succeeded = false, Reason = reason };
    }

    /// <summary>
    /// Creates a result for a recording state change whose provider outcome could not be determined.
    /// </summary>
    /// <param name="reason">The reason the outcome is unknown.</param>
    /// <returns>An indeterminate <see cref="RecordingCommandResult"/>.</returns>
    public static RecordingCommandResult Unknown(string reason)
    {
        return new RecordingCommandResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            Reason = reason,
        };
    }
}
