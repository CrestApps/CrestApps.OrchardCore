using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Orders.BackgroundTasks;

/// <summary>
/// Every 15 minutes, emails buyers who left a checkout without paying, when Settings › Commerce › Orders asks for it.
/// </summary>
[BackgroundTask(
    Title = "Checkout recovery emails",
    Schedule = "*/15 * * * *",
    Description = "Emails buyers who left a checkout without paying, once, with a link back to what they chose.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class CheckoutRecoveryBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<CheckoutRecoveryService>().SendDueAsync(cancellationToken);
}
