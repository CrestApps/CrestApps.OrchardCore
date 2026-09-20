using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Recovers due provider commands so ambiguous or interrupted provider operations are resumed through the
/// durable provider-command state machine.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Command Recovery",
    Schedule = "* * * * *",
    Description = "Recovers due Contact Center provider commands for dispatch and reconciliation.",
    LockTimeout = 5_000,
    LockExpiration = 1_800_000)]
public sealed class ProviderCommandRecoveryBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IProviderCommandRecoveryCycle>().RunAsync(cancellationToken);
}
