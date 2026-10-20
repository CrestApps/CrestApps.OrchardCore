using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.BackgroundTasks;

/// <summary>
/// Every minute: counts a batch of the orders paid before Marketing was turned on into customers' purchases, and sends
/// the next batch of each campaign that's sending or whose time has come.
/// </summary>
[BackgroundTask(
    Title = "Marketing",
    Schedule = "* * * * *",
    Description = "Builds customers' purchases from earlier orders, and sends campaigns in batches.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class MarketingBackgroundTask : IBackgroundTask
{
    // Batches of each campaign a run sends, so one big campaign doesn't hold the task.
    private const int BatchesPerRun = 10;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<MarketingBackgroundTask>>();

        try
        {
            await serviceProvider.GetRequiredService<CustomerPurchasesService>().BackfillAsync(cancellationToken);

            var baseUrl = (await serviceProvider.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).BaseUrl;

            // Without the site's address there's no unsubscribe link, so nothing goes out.
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return;
            }

            var campaigns = serviceProvider.GetRequiredService<CampaignService>();
            var due = await serviceProvider.GetRequiredService<ICampaignStore>().ListDueAsync(serviceProvider.GetRequiredService<IClock>().UtcNow, cancellationToken);

            foreach (var campaign in due)
            {
                for (var batch = 0; batch < BatchesPerRun && await campaigns.SendBatchAsync(campaign, token => UnsubscribeUrl(baseUrl, token), cancellationToken); batch++)
                {
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while building customers' purchases or sending campaigns.");
        }
    }

    internal static string UnsubscribeUrl(string baseUrl, string token)
        => $"{baseUrl.TrimEnd('/')}/marketing/unsubscribe?token={Uri.EscapeDataString(token)}";
}
