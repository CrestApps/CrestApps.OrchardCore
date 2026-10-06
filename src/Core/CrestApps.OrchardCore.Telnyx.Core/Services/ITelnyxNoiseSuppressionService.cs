namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Starts Telnyx noise suppression on a leg once it is connected, as the Telnyx settings ask.
/// </summary>
public interface ITelnyxNoiseSuppressionService
{
    /// <summary>
    /// Starts noise suppression on the leg with the configured engine, cleaning the voices the settings select. Does
    /// nothing when suppression is off or no voice is selected. Best effort: a refusal or a failure is logged and never
    /// thrown, because the call works without it.
    /// </summary>
    /// <param name="callControlId">The leg to start it on.</param>
    /// <param name="leg">Whose leg it is, which decides the direction that cleans each voice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ApplyAsync(string callControlId, TelnyxNoiseSuppressionLeg leg, CancellationToken cancellationToken = default);
}
