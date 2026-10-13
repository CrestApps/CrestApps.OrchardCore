using CrestApps.OrchardCore.Carts.Core.Services;
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
        => serviceProvider.GetRequiredService<ExpiredCartCleaner>().DeleteExpiredAsync(cancellationToken: cancellationToken);
}
