using CrestApps.OrchardCore.Surveys.Core.Runtime;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Hourly upkeep of responses: abandons idle responses, applies retention, deletes old participation records and
/// submits responses whose deadline was missed.
/// </summary>
[BackgroundTask(
    Title = "Survey upkeep",
    Schedule = "17 * * * *",
    Description = "Abandons idle survey responses, applies retention and removes old participation records.",
    LockTimeout = 1_000,
    LockExpiration = 900_000)]
public sealed class SurveyMaintenanceBackgroundTask : IBackgroundTask
{
    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<SurveyMaintenanceService>().RunAsync(cancellationToken: cancellationToken);
}
