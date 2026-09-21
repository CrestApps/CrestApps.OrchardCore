using CrestApps.OrchardCore.Asterisk.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using CrestApps.Core.Telephony.Asterisk;
using CrestApps.Core.Telephony.Asterisk.Services;
using CrestApps.Core.Telephony.Asterisk.Models;
using CrestApps.Core.Telephony.Asterisk.Data.YesSql.Indexes;
using CrestApps.Core.Telephony.Asterisk.Data.YesSql.Migrations;

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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAsteriskInboundReconciliationCycle>().RunAsync(cancellationToken);
}
