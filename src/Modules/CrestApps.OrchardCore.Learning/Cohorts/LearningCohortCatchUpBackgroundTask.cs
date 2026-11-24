using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Learning.Cohorts;

/// <summary>
/// Every hour: enrolls the members of linked cohorts that a missed membership change left out, and ends what a cohort
/// gave people who are no longer in it (<see cref="CohortLearningSync.CatchUpAsync"/>).
/// </summary>
[BackgroundTask(
    Title = "Learning cohort catch-up",
    Schedule = "45 * * * *",
    Description = "Enrolls members of linked cohorts the sync missed, and ends what a cohort gave people who left it.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class LearningCohortCatchUpBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            await serviceProvider.GetRequiredService<CohortLearningSync>().CatchUpAsync(cancellationToken);
        });
    }
}
