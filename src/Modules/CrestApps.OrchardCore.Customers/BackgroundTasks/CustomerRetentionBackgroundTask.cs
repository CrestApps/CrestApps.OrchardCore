using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.BackgroundTasks;

/// <summary>
/// Once a day, erases the personal data of guests who have not been back within the retention window, and removes
/// who placed guest orders older than the order window. Each run handles a batch, so a large backlog is worked off
/// over a few days.
/// </summary>
[BackgroundTask(
    Title = "Customer data retention",
    Schedule = "30 3 * * *",
    Description = "Erases inactive guests' personal data and anonymizes old guest orders, per Settings › Commerce › Customers.",
    LockTimeout = 5_000,
    LockExpiration = 1_800_000)]
public sealed class CustomerRetentionBackgroundTask : IBackgroundTask
{
    internal const int BatchSize = 500;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<CustomerRetentionBackgroundTask>>();

        try
        {
            var settings = await serviceProvider.GetRequiredService<ISiteService>().GetSettingsAsync<CustomerPrivacySettings>();
            var now = serviceProvider.GetRequiredService<IClock>().UtcNow;

            var guests = await ExpireGuestsAsync(
                settings,
                now,
                serviceProvider.GetRequiredService<ICustomerStore>(),
                serviceProvider.GetRequiredService<ICustomerPrivacyService>(),
                cancellationToken);

            var orders = await AnonymizeGuestOrdersAsync(settings, now, serviceProvider.GetService<OrderAnonymizer>(), cancellationToken);

            if ((guests > 0 || orders > 0) && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Customer data retention erased {GuestCount} inactive guests and anonymized {OrderCount} guest orders.", guests, orders);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying the customer data retention windows.");
        }
    }

    internal static async Task<int> ExpireGuestsAsync(CustomerPrivacySettings settings, DateTime now, ICustomerStore store, ICustomerPrivacyService privacy, CancellationToken cancellationToken)
    {
        if (settings.InactiveGuestMonths <= 0)
        {
            return 0;
        }

        // Merged guests are included: their orders moved to the account, and what remains is the guest's own record.
        var query = new CustomerQuery
        {
            HasAccount = false,
            LastActivityBefore = now.AddMonths(-settings.InactiveGuestMonths),
            NotAnonymized = true,
        };

        var count = 0;

        while (count < BatchSize)
        {
            // Each erased guest drops out of the query, so every page starts from the beginning again.
            var (_, customers) = await store.PageAsync(query, 0, Math.Min(50, BatchSize - count), cancellationToken);

            if (customers.Count == 0)
            {
                break;
            }

            foreach (var customer in customers)
            {
                await privacy.ExpireAsync(customer, cancellationToken);
                count++;
            }
        }

        return count;
    }

    internal static Task<int> AnonymizeGuestOrdersAsync(CustomerPrivacySettings settings, DateTime now, OrderAnonymizer anonymizer, CancellationToken cancellationToken)
    {
        if (anonymizer is null || settings.GuestOrderMonths <= 0)
        {
            return Task.FromResult(0);
        }

        // Orders still owed keep their contact until they are settled or canceled.
        return anonymizer.AnonymizeAsync(new OrderQuery
        {
            GuestsOnly = true,
            CreatedBefore = now.AddMonths(-settings.GuestOrderMonths),
            Statuses = [OrderStatus.Paid, OrderStatus.Canceled],
        }, BatchSize, cancellationToken);
    }
}
