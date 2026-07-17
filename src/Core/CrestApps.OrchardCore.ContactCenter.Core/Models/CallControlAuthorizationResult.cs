namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes the outcome of the shared call-control authorization boundary.
/// </summary>
public sealed class CallControlAuthorizationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command is authorized.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the redacted failure reason safe to return to clients.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the resolved agent profile identifier for the caller.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the resolved provider call identifier.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the resolved call session.
    /// </summary>
    public CallSession CallSession { get; set; }

    /// <summary>
    /// Creates an authorized result.
    /// </summary>
    /// <param name="agentId">The resolved agent identifier.</param>
    /// <param name="session">The resolved call session.</param>
    /// <returns>The authorized result.</returns>
    public static CallControlAuthorizationResult Success(string agentId, CallSession session)
    {
        return new CallControlAuthorizationResult
        {
            Succeeded = true,
            AgentId = agentId,
            ProviderCallId = session.ProviderCallId,
            CallSession = session,
        };
    }

    /// <summary>
    /// Creates a redacted denial result.
    /// </summary>
    /// <returns>The denied result.</returns>
    public static CallControlAuthorizationResult Denied()
    {
        return new CallControlAuthorizationResult
        {
            Succeeded = false,
            FailureReason = "The requested call is not available.",
        };
    }
}
