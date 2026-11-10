using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Classifieds.BackgroundTasks;

/// <summary>
/// Every five minutes: takes down listings whose term ran out, and tells their posters.
/// </summary>
[BackgroundTask(
    Title = "Listing terms",
    Schedule = "*/5 * * * *",
    Description = "Takes down posted listings whose term ran out, and emails their posters to renew them.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class ClassifiedsBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var expired = await serviceProvider.GetRequiredService<ClassifiedService>().ExpireDueAsync(200, cancellationToken);

            run.AddItems(expired, "expired");
        });
    }
}
