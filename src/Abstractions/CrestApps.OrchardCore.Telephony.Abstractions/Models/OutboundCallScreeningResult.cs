namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents the outcome of screening an outbound origination. A denied result must never be dispatched
/// to a provider.
/// </summary>
public sealed class OutboundCallScreeningResult
{
    /// <summary>
    /// Gets a value indicating whether the origination is permitted to proceed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the stable machine-readable code describing why the origination was denied, when
    /// <see cref="IsAllowed"/> is <see langword="false"/>.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets the human-readable description of why the origination was denied, when <see cref="IsAllowed"/>
    /// is <see langword="false"/>.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Creates a result that permits the origination to proceed.
    /// </summary>
    /// <returns>An allowing <see cref="OutboundCallScreeningResult"/>.</returns>
    public static OutboundCallScreeningResult Allow()
        => new() { IsAllowed = true };

    /// <summary>
    /// Creates a result that denies the origination.
    /// </summary>
    /// <param name="reason">The stable machine-readable denial code.</param>
    /// <param name="description">The human-readable denial description.</param>
    /// <returns>A denying <see cref="OutboundCallScreeningResult"/>.</returns>
    public static OutboundCallScreeningResult Deny(string reason, string description)
    {
        return new OutboundCallScreeningResult
        {
            IsAllowed = false,
            Reason = reason,
            Description = description,
        };
    }
}
