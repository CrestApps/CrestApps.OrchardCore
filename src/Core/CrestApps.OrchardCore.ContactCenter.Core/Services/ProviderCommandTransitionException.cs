using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The exception thrown when a provider command is asked to move between two states that the durable state
/// machine does not permit. The command is never mutated when this exception is raised.
/// </summary>
public sealed class ProviderCommandTransitionException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCommandTransitionException"/> class.
    /// </summary>
    /// <param name="commandId">The stable identifier of the command that could not transition.</param>
    /// <param name="from">The current status of the command.</param>
    /// <param name="to">The requested target status.</param>
    public ProviderCommandTransitionException(
        string commandId,
        ProviderCommandStatus from,
        ProviderCommandStatus to)
        : base($"The provider command '{commandId}' cannot transition from '{from}' to '{to}'.")
    {
        CommandId = commandId;
        From = from;
        To = to;
    }

    /// <summary>
    /// Gets the stable identifier of the command that could not transition.
    /// </summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets the current status of the command.
    /// </summary>
    public ProviderCommandStatus From { get; }

    /// <summary>
    /// Gets the requested target status.
    /// </summary>
    public ProviderCommandStatus To { get; }
}
