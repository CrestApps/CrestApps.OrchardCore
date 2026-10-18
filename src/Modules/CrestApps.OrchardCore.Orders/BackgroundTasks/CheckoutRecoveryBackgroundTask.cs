using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        try
        {
            await serviceProvider.GetRequiredService<CheckoutRecoveryService>().SendDueAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            serviceProvider.GetRequiredService<ILogger<CheckoutRecoveryBackgroundTask>>()
                .LogError(exception, "An error occurred while sending checkout recovery emails.");
        }
    }
}
