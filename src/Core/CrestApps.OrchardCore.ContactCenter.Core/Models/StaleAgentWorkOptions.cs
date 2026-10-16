namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Defines when an agent's work is old enough to be called stale.
/// </summary>
/// <remarks>
/// The grace period for presence, reservations and queue items is
/// <see cref="AgentAvailabilityOptions.OrphanedBusyGracePeriod"/>.
/// </remarks>
public sealed class StaleAgentWorkOptions
{
    /// <summary>
    /// Gets or sets how long a soft-phone call must have been in progress before it can be called stale. A call that was
    /// only just placed is still being propagated by the provider.
    /// </summary>
    public TimeSpan CallMinimumAge { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how long a call must have been in progress before a provider that cannot answer for it at all
    /// (rather than saying it is gone) makes it stale.
    /// </summary>
    public TimeSpan UnresolvedCallAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long a call the browser placed itself may go unreported, with no live soft-phone session, before
    /// it is called stale.
    /// </summary>
    public TimeSpan ClientRecordedCallSilence { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long a call may be in progress, with no live soft-phone session behind it, before it is called
    /// stale although the provider has not said it is gone.
    /// </summary>
    public TimeSpan CallMaximumAge { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Gets or sets how long the provider is given to answer about one call before it is treated as unable to.
    /// </summary>
    public TimeSpan ProviderLookupTimeout { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets or sets how long a provider's answer about a call is reused by the dashboard before it is asked again.
    /// Releasing an agent's work never reuses an answer.
    /// </summary>
    public TimeSpan ProviderAnswerCacheDuration { get; set; } = TimeSpan.FromSeconds(30);
}
