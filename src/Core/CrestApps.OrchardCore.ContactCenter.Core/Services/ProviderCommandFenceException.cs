namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The exception thrown when an ownership-guarded provider command transition presents a stale or unknown
/// fence token or owner token. The command is never mutated when this exception is raised, so a superseded
/// worker cannot corrupt the durable state.
/// </summary>
public sealed class ProviderCommandFenceException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandFenceException"/> class.
    /// </summary>
    /// <param name="commandId">The stable identifier of the command that rejected the transition.</param>
    /// <param name="expectedFenceToken">The fence token currently held by the command.</param>
    /// <param name="providedFenceToken">The stale fence token that was presented.</param>
    public ProviderCommandFenceException(
        string commandId,
        long expectedFenceToken,
        long providedFenceToken)
        : base($"The provider command '{commandId}' rejected a fenced transition. Expected fence token '{expectedFenceToken}' but received '{providedFenceToken}'.")
    {
        CommandId = commandId;
        ExpectedFenceToken = expectedFenceToken;
        ProvidedFenceToken = providedFenceToken;
    }

    /// <summary>
    /// Gets the stable identifier of the command that rejected the transition.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets the fence token currently held by the command.
    /// </summary>
    public long ExpectedFenceToken { get; }

    /// <summary>
    /// Gets the stale fence token that was presented.
    /// </summary>
    public long ProvidedFenceToken { get; }
}
