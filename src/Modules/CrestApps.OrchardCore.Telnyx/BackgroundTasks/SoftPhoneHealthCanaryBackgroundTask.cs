using CrestApps.OrchardCore.Telnyx.Core.Services;
using Microsoft.Extensions.DependencyInjection;
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
        => serviceProvider.GetRequiredService<ISoftPhoneHealthCanaryCycle>().RunAsync(cancellationToken);
}
