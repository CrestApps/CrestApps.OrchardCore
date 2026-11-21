using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Every two minutes: grants the paid learning purchases and takes back the refunded ones whose work is still due,
/// such as a grant that met a busy enrollment or a request that ended before its work ran.
/// </summary>
[BackgroundTask(
    Title = "Learning purchase fulfilment",
    Schedule = "*/2 * * * *",
    Description = "Grants paid learning purchases and takes back refunded ones that are still waiting.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
internal sealed class LearningFulfillmentBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<LearningPurchaseFulfillment>().RunDueAsync(cancellationToken);
}
