namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Receives what became of a transfer to an outside number after the provider accepted it: whether the far end
/// answered, or the leg it rang failed.
/// </summary>
/// <remarks>
/// A provider accepting a transfer command only means it has started ringing the destination. The destination can
/// still be busy, not answer, or reject the call, and the caller is then still on the original leg with nobody on the
/// other end. Treating the acceptance as the transfer having happened dropped that caller.
/// </remarks>
public interface IExternalTransferOutcomeSink
{
    /// <summary>
    /// Applies the outcome of a transfer's destination leg.
    /// </summary>
    /// <param name="outcome">What happened to the destination leg, and for which call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the outcome belonged to a transfer that was waiting for it.</returns>
    Task<bool> HandleAsync(ExternalTransferOutcome outcome, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a provider reported about the leg a transfer rang.
/// </summary>
public sealed class ExternalTransferOutcome
{
    /// <summary>
    /// Gets or sets the technical name of the provider that reported it.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the Contact Center interaction the transfer was made for.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the destination leg.
    /// </summary>
    public string ProviderLegId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the destination answered. <see langword="false"/> means the leg ended
    /// before it was answered.
    /// </summary>
    public bool Answered { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the leg ended because the caller hung up while it was ringing, so there
    /// is nobody left to put somewhere else.
    /// </summary>
    public bool CallerLeft { get; set; }

    /// <summary>
    /// Gets or sets the provider's own reason the leg ended, such as <c>user_busy</c> or <c>timeout</c>.
    /// </summary>
    public string HangupCause { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for this delivery.
    /// </summary>
    public string DeliveryId { get; set; }
}
