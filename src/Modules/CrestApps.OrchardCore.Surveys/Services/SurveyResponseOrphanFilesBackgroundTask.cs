using CrestApps.OrchardCore.Surveys.Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Removes, every hour, the files of survey responses that no answer kept a day after they were stored, such as the
/// files of a deleted response or of a request that failed after storing them (<see cref="SurveyResponseOrphanFileSweeper"/>).
/// </summary>
[BackgroundTask(
    Title = "Remove survey files no answer kept",
    Schedule = "47 * * * *",
    Description = "Removes the files of survey responses that no answer kept a day after they were stored.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class SurveyResponseOrphanFilesBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<SurveyResponseOrphanFileSweeper>().SweepAsync(cancellationToken);
}
