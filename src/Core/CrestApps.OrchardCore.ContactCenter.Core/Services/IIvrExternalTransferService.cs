using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Sends a caller from an entry point's phone menu to one of the tenant's approved external destinations.
/// </summary>
public interface IIvrExternalTransferService
{
    /// <summary>
    /// Transfers the caller to the approved external destination the menu named. Only an enabled destination from
    /// the tenant's approved catalog that the dial policy allows, and that is not one of the contact center's own
    /// numbers, is reachable; the menu never supplies a number of its own.
    /// </summary>
    /// <remarks>
    /// On a provider that reports what became of the leg it rang, the call is only settled as transferred once that
    /// leg answers (<see cref="CompleteAsync"/>); until then it waits, and a leg that fails is reported through
    /// <see cref="FailAsync"/>. On any other provider the provider accepting the transfer settles it.
    /// </remarks>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="destinationId">The identifier of the approved destination.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the provider took the transfer; otherwise <see langword="false"/>, and
    /// the caller is still on the line for the menu to put somewhere else.</returns>
    Task<bool> TransferAsync(Interaction interaction, string destinationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles a transfer that was waiting for its destination to answer, now that it has.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a transfer was waiting and is now settled.</returns>
    Task<bool> CompleteAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the destination of a waiting transfer never answered, and forgets the transfer so the caller can
    /// be put somewhere else.
    /// </summary>
    /// <param name="interaction">The caller's interaction.</param>
    /// <param name="hangupCause">The provider's reason the destination leg ended.</param>
    /// <param name="callerLeft">Whether the leg ended because the caller hung up while it rang.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifier of the destination that failed, or <see langword="null"/> when no transfer was waiting.</returns>
    Task<string> FailAsync(Interaction interaction, string hangupCause, bool callerLeft, CancellationToken cancellationToken = default);
}
