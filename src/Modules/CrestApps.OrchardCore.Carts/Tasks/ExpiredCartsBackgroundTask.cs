using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Carts.Tasks;

[BackgroundTask(
    Title = "Expired Carts Cleanup",
    Schedule = "15 * * * *",
    Description = "Deletes carts nobody has changed within their lifetime.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class ExpiredCartsBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var deleted = await serviceProvider.GetRequiredService<ExpiredCartCleaner>().DeleteExpiredAsync(cancellationToken: cancellationToken);

            run.AddItems(deleted, "deleted");
        });
    }
}
