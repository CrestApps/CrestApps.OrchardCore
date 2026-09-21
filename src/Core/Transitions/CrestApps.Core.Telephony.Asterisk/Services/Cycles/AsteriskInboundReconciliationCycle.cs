using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Telephony.Asterisk.Services;

/// <summary>
/// Periodically reconciles this tenant's durable Asterisk channel bindings against live ARI state so a stranded
/// resource is recovered even when the real-time listener's WebSocket never dropped. Listener-triggered
/// reconciliation only runs on reconnect, so a transient ARI REST failure — or a connect that crashed mid-flight —
/// could otherwise leave a <see cref="Models.AsteriskChannelBindingState.Terminating"/> record, a channel whose
/// terminal event was missed, or an orphaned <see cref="Models.AsteriskChannelBindingState.Pending"/> agent leg
/// unresolved indefinitely while the socket stayed healthy. The sweep resolves the tenant-scoped reconcilers and is
/// a no-op for tenants with no Asterisk bindings, so an idle or unconfigured tenant pays only one indexed lookup.
/// </summary>
public sealed class AsteriskInboundReconciliationCycle : IAsteriskInboundReconciliationCycle
{
    private readonly IEnumerable<IAsteriskProviderStateReconciler> _reconcilers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskInboundReconciliationCycle"/> class.
    /// </summary>
    /// <param name="reconcilers">The reconcilers.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskInboundReconciliationCycle(
        IEnumerable<IAsteriskProviderStateReconciler> reconcilers,
        ILogger<AsteriskInboundReconciliationCycle> logger)
    {
        _reconcilers = reconcilers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // The service provider is the tenant's scoped container (Orchard runs background tasks per tenant), so the
        // resolved _reconcilers, binding store, and ARI client are all scoped to this tenant — the sweep can never
        // read or act on another tenant's channels. Every configured tenant listener reconciles under the canonical
        // voice provider technical name, and both inbound and agent-leg bindings are stamped with it, so the sweep
        // uses the same name to match this tenant's Asterisk bindings.

        foreach (var reconciler in _reconcilers)
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
                _logger.LogError(
                    ex,
                    "The periodic Asterisk channel binding reconciliation sweep failed for provider {ProviderName}.",
                    AsteriskConstants.ProviderTechnicalName);
            }
        }
    }
}
