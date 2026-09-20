using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Drains every Contact Center table of records beyond its configured data-governance retention window once a
/// day.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Data Retention",
    Schedule = "0 3 * * *",
    Description = "Purges Contact Center records older than their configured retention windows.",
    LockTimeout = 10_000,
    LockExpiration = 300_000)]
public sealed class ContactCenterRetentionBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IContactCenterRetentionCycle>().RunAsync(cancellationToken);
}
