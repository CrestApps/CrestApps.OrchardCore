using CrestApps.OrchardCore.Telnyx.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telnyx.BackgroundTasks;

/// <summary>
/// Asks the provider what calls it actually has up, and acts on the ones this platform has no record of.
/// </summary>
/// <remarks>
/// The interaction reconciliation task walks the platform's own records, so it can only repair calls there is a
/// record of. This is the other direction, and it is the only way to see a call that was placed immediately
/// before a restart: no interaction was written, so no local sweep will ever reach it, and the person on it is
/// connected to a platform that does not know they are there.
/// </remarks>
[BackgroundTask(
    Title = "Telnyx Orphaned Call Reconciliation",
    Schedule = "*/5 * * * *",
    Description = "Finds calls the Telnyx connection has up that this platform has no interaction for.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class TelnyxOrphanedCallReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ITelnyxOrphanedCallReconciliationCycle>().RunAsync(cancellationToken);
}
