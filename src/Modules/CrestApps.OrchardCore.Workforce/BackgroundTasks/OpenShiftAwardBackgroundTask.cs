using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Workforce.BackgroundTasks;

/// <summary>
/// Awards the open shifts given by seniority whose claim deadline has passed.
/// </summary>
[BackgroundTask(
    Title = "Open shifts awarded by seniority",
    Schedule = "*/5 * * * *",
    Description = "Gives each open shift whose claim deadline has passed to its most senior eligible claimant.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class OpenShiftAwardBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return serviceProvider.GetRequiredService<IOpenShiftService>().AwardDueAsync(cancellationToken);
    }
}
