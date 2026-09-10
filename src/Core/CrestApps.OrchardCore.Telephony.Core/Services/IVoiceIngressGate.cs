namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Serializes every normalized voice event for one provider call stream behind a single distributed lock.
/// <para>
/// The gate is the only component permitted to acquire the ingestion lock. Once a call stream is gated, any
/// nested request for the same stream — including one raised from a fresh shell scope opened while the outer
/// ingestion is still in flight — is satisfied re-entrantly instead of contending with the lease that is
/// already held. Without that guarantee, fanning one event out to several projections would either take the
/// lock once per projection or dead-lock a projection against its own caller.
/// </para>
/// </summary>
public interface IVoiceIngressGate
{
    /// <summary>
    /// Acquires the ingestion lease for the specified provider call stream.
    /// </summary>
    /// <param name="providerName">The canonical provider name that owns the call stream.</param>
    /// <param name="providerCallId">The provider-specific call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A lease that must be disposed once ingestion completes. Disposing a re-entrant lease releases nothing,
    /// because the outermost lease still owns the underlying distributed lock.
    /// </returns>
    /// <exception cref="TimeoutException">Thrown when the ingestion lock cannot be acquired in time.</exception>
    Task<IAsyncDisposable> AcquireAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the current asynchronous flow already holds the ingestion lease for the specified
    /// provider call stream.
    /// </summary>
    /// <param name="providerName">The canonical provider name that owns the call stream.</param>
    /// <param name="providerCallId">The provider-specific call identifier.</param>
    /// <returns><see langword="true"/> when the lease is already held; otherwise, <see langword="false"/>.</returns>
    bool IsHeld(string providerName, string providerCallId);
}
