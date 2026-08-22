namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the result of a Contact Center call command such as accepting or declining an offered
/// interaction. It tells the caller whether the command succeeded and whether the agent's device must
/// still answer the media (for providers whose delivery model rings the agent's device directly).
/// </summary>
public sealed class CallCommandResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the human-readable outcome of the command.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent's device must answer the media after the
    /// command. This is <see langword="true"/> for agent-device-native providers, where the live call
    /// already rings the agent's device, and <see langword="false"/> for server-side ACD providers,
    /// where the Contact Center already bridged the call to the agent.
    /// </summary>
    public bool RequiresDeviceAnswer { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the command applied to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the call session the command applied to, when applicable.
    /// </summary>
    public string CallSessionId { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="reason">The human-readable outcome.</param>
    /// <param name="requiresDeviceAnswer">Whether the agent's device must answer the media.</param>
    /// <returns>The successful result.</returns>
    public static CallCommandResult Success(string reason, bool requiresDeviceAnswer)
    {
        return new CallCommandResult
        {
            Succeeded = true,
            Reason = reason,
            RequiresDeviceAnswer = requiresDeviceAnswer,
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">The human-readable failure reason.</param>
    /// <returns>The failed result.</returns>
    public static CallCommandResult Failure(string reason)
    {
        return new CallCommandResult
        {
            Succeeded = false,
            Reason = reason,
        };
    }
}
