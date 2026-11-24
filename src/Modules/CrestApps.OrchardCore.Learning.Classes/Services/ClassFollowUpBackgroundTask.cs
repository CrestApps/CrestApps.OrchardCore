using CrestApps.OrchardCore.Learning.Classes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Every two minutes: does again the work on class registrations that did not finish when their appointment changed,
/// such as attendance that met a busy or suspended enrollment, or class meetings that met a busy shift plan.
/// </summary>
[BackgroundTask(
    Title = "Learning class follow-ups",
    Schedule = "*/2 * * * *",
    Description = "Records class attendance and shift plan changes that could not be made at the time.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
internal sealed class ClassFollowUpBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ClassFollowUps>().RunDueAsync(cancellationToken);
}
