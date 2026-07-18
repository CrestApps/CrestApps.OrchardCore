using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Asterisk.BackgroundTasks;

/// <summary>
/// Periodically reconciles this tenant's durable Asterisk channel bindings against live ARI state so a stranded
/// resource is recovered even when the real-time listener's WebSocket never dropped. Listener-triggered
/// reconciliation only runs on reconnect, so a transient ARI REST failure — or a connect that crashed mid-flight —
/// could otherwise leave a <see cref="Models.AsteriskChannelBindingState.Terminating"/> record, a channel whose
/// terminal event was missed, or an orphaned <see cref="Models.AsteriskChannelBindingState.Pending"/> agent leg
/// unresolved indefinitely while the socket stayed healthy. The sweep resolves the tenant-scoped reconcilers and is
/// a no-op for tenants with no Asterisk bindings, so an idle or unconfigured tenant pays only one indexed lookup.
/// </summary>
[BackgroundTask(
    Title = "Asterisk Channel Binding Reconciliation",
    Schedule = "* * * * *",
    Description = "Reconciles durable Asterisk channel bindings against live ARI state to recover stranded call resources.",
    LockTimeout = 3_000,
    LockExpiration = 120_000)]
public sealed class AsteriskInboundReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        // The service provider is the tenant's scoped container (Orchard runs background tasks per tenant), so the
        // resolved reconcilers, binding store, and ARI client are all scoped to this tenant — the sweep can never
        // read or act on another tenant's channels. Every configured tenant listener reconciles under the canonical
        // voice provider technical name, and both inbound and agent-leg bindings are stamped with it, so the sweep
        // uses the same name to match this tenant's Asterisk bindings.
        var reconcilers = serviceProvider.GetServices<IAsteriskProviderStateReconciler>();
        var logger = serviceProvider.GetRequiredService<ILogger<AsteriskInboundReconciliationBackgroundTask>>();

        foreach (var reconciler in reconcilers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await reconciler.ReconcileAsync(AsteriskConstants.ProviderTechnicalName, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    OperationalLogRedactor.RedactException(ex),
                    "The periodic Asterisk channel binding reconciliation sweep failed for provider {ProviderName}.",
                    AsteriskConstants.ProviderTechnicalName);
            }
        }
    }
}
