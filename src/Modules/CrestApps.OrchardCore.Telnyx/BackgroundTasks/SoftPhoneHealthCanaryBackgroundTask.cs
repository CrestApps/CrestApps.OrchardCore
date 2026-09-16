using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telnyx.BackgroundTasks;

/// <summary>
/// Periodically evaluates soft-phone health -- browser credential issuance (a proxy for registration success)
/// and inbound webhook processing -- and logs a snapshot, warning when the credential issuance success rate
/// falls below the alert threshold so a broken registration path surfaces without waiting for an agent to
/// report it. It is a passive canary: a full round-trip <em>audio</em> canary needs a real browser (WebRTC),
/// which a server background task cannot run, so the browser diagnostics "Run audio test" (against the
/// configured echo destination) provides the on-demand audio check. The sweep is a no-op when Telnyx is not
/// configured.
/// </summary>
[BackgroundTask(
    Title = "Soft Phone Health Canary",
    Schedule = "*/5 * * * *",
    Description = "Logs soft-phone credential-issuance and webhook health, and warns when the registration success rate drops.",
    LockTimeout = 3_000,
    LockExpiration = 60_000)]
public sealed class SoftPhoneHealthCanaryBackgroundTask : IBackgroundTask
{
    // Alert when at least this many credential issuance attempts have accumulated and the success rate has
    // fallen below the threshold, so a single early failure does not trip the alert.
    private const long MinCredentialAttemptsBeforeAlert = 5;
    private const double CredentialSuccessRateAlertThreshold = 0.8;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<TelnyxOptions>>().CurrentValue;

        // Nothing to canary when the provider is not configured.
        if (!options.IsConfigured)
        {
            return Task.CompletedTask;
        }

        var metrics = serviceProvider.GetRequiredService<ISoftPhoneHealthMetrics>();
        var logger = serviceProvider.GetRequiredService<ILogger<SoftPhoneHealthCanaryBackgroundTask>>();
        var snapshot = metrics.GetSnapshot();
        var credentialAttempts = snapshot.CredentialsIssued + snapshot.CredentialFailures;

        if (credentialAttempts >= MinCredentialAttemptsBeforeAlert &&
            snapshot.CredentialSuccessRate < CredentialSuccessRateAlertThreshold)
        {
            logger.LogWarning(
                "Soft phone health canary: credential issuance success rate {Rate:P0} ({Issued} issued / {Failures} failed) is below the alert threshold since {Since:o}. Webhooks: {WebhooksOk} processed / {WebhookFailures} failed.",
                snapshot.CredentialSuccessRate,
                snapshot.CredentialsIssued,
                snapshot.CredentialFailures,
                snapshot.SinceUtc,
                snapshot.WebhooksProcessed,
                snapshot.WebhookFailures);

            return Task.CompletedTask;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
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
