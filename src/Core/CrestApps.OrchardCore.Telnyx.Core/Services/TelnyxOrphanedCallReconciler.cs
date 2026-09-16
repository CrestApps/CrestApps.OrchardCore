using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Finds calls the provider has up that the platform has no record of, and does what the deployment asked about
/// them.
/// <para>
/// Reconciliation from local records can only repair calls there is a record of. A call placed just before the
/// process died has none: the person is connected, no webhook will ever create a record for them because the
/// events are correlated by an interaction that does not exist, and nothing else in the system will ever notice.
/// Asking the provider what is actually up on the connection is the only way to see them.
/// </para>
/// </summary>
public sealed class TelnyxOrphanedCallReconciler
{
    /// <summary>
    /// How long a call must have been up before it is treated as lost. A call that has been running for seconds
    /// is far more likely to be one whose interaction is still being written than one that was orphaned, and
    /// acting on it would end calls that are working.
    /// </summary>
    private static readonly TimeSpan _grace = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A cap on how many pages one pass walks, so a connection with an unexpected number of calls cannot turn
    /// this into an unbounded loop against the provider's API.
    /// </summary>
    private const int MaxPages = 20;

    private const string ApologyText =
        "We're sorry. This call can no longer be completed because of a system interruption. Please call us back. Goodbye.";

    private readonly TelnyxApiClient _apiClient;
    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxOrphanedCallReconciler"/> class.
    /// </summary>
    /// <param name="apiClient">The typed Telnyx client.</param>
    /// <param name="interactionStore">The store that says which calls the platform knows about.</param>
    /// <param name="options">The Telnyx options.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxOrphanedCallReconciler(
        TelnyxApiClient apiClient,
        ITelephonyInteractionStore interactionStore,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<TelnyxOrphanedCallReconciler> logger)
    {
        _apiClient = apiClient;
        _interactionStore = interactionStore;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Lists the calls the provider has up and acts on the ones the platform does not know about.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<TelnyxOrphanedCallReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.ConnectionId))
        {
            // A tenant that has not finished configuring Telnyx has no connection to list. Asking anyway logs a
            // provider error on every pass and tells nobody anything.
            return new TelnyxOrphanedCallReconciliationResult { Succeeded = false };
        }

        var found = 0;
        var ended = 0;
        string pageToken = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var listing = await _apiClient.ListActiveCallsAsync(options.ConnectionId, pageToken, cancellationToken);

            if (!listing.Succeeded)
            {
                // A refused listing says nothing about the calls. Reading it as "everything is orphaned" and
                // acting on that would hang up every live call on an expired API key.
                _logger.LogWarning(
                    "Could not list the active calls on the Telnyx connection ({StatusCode}); orphaned-call reconciliation is skipped this pass.",
                    listing.StatusCode);

                return new TelnyxOrphanedCallReconciliationResult
                {
                    Succeeded = false,
                    OrphansFound = found,
                    OrphansEnded = ended,
                };
            }

            foreach (var call in listing.Calls)
            {
                if (call.DurationSeconds < _grace.TotalSeconds)
                {
                    continue;
                }

                var known = await _interactionStore.FindByProviderCallIdAsync(
                    TelnyxConstants.ProviderTechnicalName,
                    call.CallControlId,
                    cancellationToken);

                if (known is not null)
                {
                    continue;
                }

                found++;

                _logger.LogWarning(
                    "The Telnyx connection has a call '{CallId}' up for {DurationSeconds}s that this platform has no interaction for. It was most likely placed immediately before a restart.",
                    call.CallControlId.SanitizeLogValue(),
                    call.DurationSeconds);

                if (options.OrphanedCallHandling == TelnyxOrphanedCallHandling.EndCall &&
                    await EndOrphanAsync(call.CallControlId, cancellationToken))
                {
                    ended++;
                }
            }

            pageToken = listing.NextPageToken;

            if (string.IsNullOrEmpty(pageToken))
            {
                break;
            }
        }

        return new TelnyxOrphanedCallReconciliationResult
        {
            Succeeded = true,
            OrphansFound = found,
            OrphansEnded = ended,
        };
    }

    private async Task<bool> EndOrphanAsync(string callControlId, CancellationToken cancellationToken)
    {
        // Speak first. Someone whose call ends mid-sentence with no explanation calls straight back, and the
        // second call is as likely to fail as the first.
        await _apiClient.SpeakAsync(callControlId, ApologyText, cancellationToken: cancellationToken);

        var hangup = await _apiClient.HangupAsync(callControlId, cancellationToken);

        if (!hangup.Succeeded)
        {
            _logger.LogWarning(
                "Could not end the orphaned Telnyx call '{CallId}' ({StatusCode}).",
                callControlId.SanitizeLogValue(),
                hangup.StatusCode);
        }

        return hangup.Succeeded;
    }
}

/// <summary>
/// What one reconciliation pass found.
/// </summary>
public sealed class TelnyxOrphanedCallReconciliationResult
{
    /// <summary>
    /// Gets a value indicating whether the provider could be asked at all. When this is false the counts say
    /// nothing about how many orphans there are.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets how many live calls had no interaction.
    /// </summary>
    public int OrphansFound { get; init; }

    /// <summary>
    /// Gets how many of them were ended.
    /// </summary>
    public int OrphansEnded { get; init; }
}
