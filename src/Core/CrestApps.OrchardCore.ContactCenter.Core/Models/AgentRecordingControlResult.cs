namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the outcome of an agent-initiated secure-pause or resume request on the agent's own live
/// interaction. It carries a client-safe reason and, on success, the resulting recording state so the agent
/// desktop can update its control without re-reading the interaction.
/// </summary>
public sealed class AgentRecordingControlResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the request was authorized and the recording state change applied.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider may have applied the change but its outcome could not
    /// be observed, so the agent desktop must reconcile the state rather than assume the request took effect.
    /// </summary>
    public bool OutcomeUnknown { get; set; }

    /// <summary>
    /// Gets or sets a client-safe explanation of the outcome.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether recording is paused after the request, so the caller can present
    /// the correct control state without re-reading the interaction.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="isPaused">Whether recording is paused after the request.</param>
    /// <returns>A successful <see cref="AgentRecordingControlResult"/>.</returns>
    public static AgentRecordingControlResult Success(bool isPaused)
    {
        return new AgentRecordingControlResult
        {
            Succeeded = true,
            IsPaused = isPaused,
        };
    }

    /// <summary>
    /// Creates a failed result for a request that was definitely not applied.
    /// </summary>
    /// <param name="reason">The client-safe failure reason.</param>
    /// <returns>A failed <see cref="AgentRecordingControlResult"/>.</returns>
    public static AgentRecordingControlResult Failure(string reason)
    {
        return new AgentRecordingControlResult
        {
            Succeeded = false,
            Reason = reason,
        };
    }

    /// <summary>
    /// Creates a result for a request whose provider outcome could not be determined.
    /// </summary>
    /// <param name="reason">The reason the outcome is unknown.</param>
    /// <returns>An indeterminate <see cref="AgentRecordingControlResult"/>.</returns>
    public static AgentRecordingControlResult Unknown(string reason)
    {
        return new AgentRecordingControlResult
        {
            Succeeded = false,
            OutcomeUnknown = true,
            Reason = reason,
        };
    }
}
