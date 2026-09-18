using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Core.Services;

/// <summary>
/// Periodically evaluates soft-phone health -- browser credential issuance (a proxy for registration success)
/// and inbound webhook processing -- and logs a snapshot, warning when the credential issuance success rate
/// falls below the alert threshold so a broken registration path surfaces without waiting for an agent to
/// report it. It is a passive canary: a full round-trip <em>audio</em> canary needs a real browser (WebRTC),
/// which a server background task cannot run, so the browser diagnostics "Run audio test" (against the
/// configured echo destination) provides the on-demand audio check. The sweep is a no-op when Telnyx is not
/// configured.
/// </summary>
public sealed class SoftPhoneHealthCanaryCycle : ISoftPhoneHealthCanaryCycle
{
    // Alert when at least this many credential issuance attempts have accumulated and the success rate has
    // fallen below the threshold, so a single early failure does not trip the alert.
    private const long MinCredentialAttemptsBeforeAlert = 5;
    private const double CredentialSuccessRateAlertThreshold = 0.8;

    private readonly ISoftPhoneHealthMetrics _metrics;
    private readonly IOptionsMonitor<TelnyxOptions> _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneHealthCanaryCycle"/> class.
    /// </summary>
    /// <param name="metrics">The metrics.</param>
    /// <param name="options">The Telnyx options, read per pass so a change takes effect without a restart.</param>
    /// <param name="logger">The logger.</param>
    public SoftPhoneHealthCanaryCycle(
        ISoftPhoneHealthMetrics metrics,
        IOptionsMonitor<TelnyxOptions> options,
        ILogger<SoftPhoneHealthCanaryCycle> logger)
    {
        _metrics = metrics;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        // Nothing to canary when the provider is not configured.
        if (!options.IsConfigured)
        {
            return Task.CompletedTask;
        }

        var snapshot = _metrics.GetSnapshot();
        var credentialAttempts = snapshot.CredentialsIssued + snapshot.CredentialFailures;

        if (credentialAttempts >= MinCredentialAttemptsBeforeAlert &&
            snapshot.CredentialSuccessRate < CredentialSuccessRateAlertThreshold)
        {
            _logger.LogWarning(
                "Soft phone health canary: credential issuance success rate {Rate:P0} ({Issued} issued / {Failures} failed) is below the alert threshold since {Since:o}. Webhooks: {WebhooksOk} processed / {WebhookFailures} failed.",
                snapshot.CredentialSuccessRate,
                snapshot.CredentialsIssued,
                snapshot.CredentialFailures,
                snapshot.SinceUtc,
                snapshot.WebhooksProcessed,
                snapshot.WebhookFailures);

            return Task.CompletedTask;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Soft phone health canary: credentials {Issued} issued / {Failures} failed (success {Rate:P0}); webhooks {WebhooksOk} processed / {WebhookFailures} failed (success {WebhookRate:P0}) since {Since:o}.",
                snapshot.CredentialsIssued,
                snapshot.CredentialFailures,
                snapshot.CredentialSuccessRate,
                snapshot.WebhooksProcessed,
                snapshot.WebhookFailures,
                snapshot.WebhookSuccessRate,
                snapshot.SinceUtc);
        }

        return Task.CompletedTask;
    }
}
