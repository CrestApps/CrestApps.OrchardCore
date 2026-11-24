using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Every hour: expires the enrollments whose access ended, and tells the observers about access that ends soon and
/// learners who stopped working on a course.
/// </summary>
[BackgroundTask(
    Title = "Learning upkeep",
    Schedule = "15 * * * *",
    Description = "Expires enrollments whose access ended, and raises the access-ending and inactive-learner events.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class LearningDailyBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            await serviceProvider.GetRequiredService<LearningUpkeep>().RunAsync(cancellationToken);
        });
    }
}
