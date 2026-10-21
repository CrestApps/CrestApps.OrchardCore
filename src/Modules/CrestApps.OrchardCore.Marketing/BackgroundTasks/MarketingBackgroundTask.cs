using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    LockExpiration = 600_000,
    UsePipeline = true)]
public sealed class MarketingBackgroundTask : IBackgroundTask
{
    // Batches of each campaign a run sends, so one big campaign doesn't hold the task.
    private const int BatchesPerRun = 10;

    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        await serviceProvider.GetRequiredService<CustomerPurchasesService>().BackfillAsync(cancellationToken);

        var baseUrl = (await serviceProvider.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).BaseUrl;

        // Without the site's address there's no unsubscribe link, so nothing goes out.
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        var linkGenerator = serviceProvider.GetRequiredService<LinkGenerator>();
        var campaigns = serviceProvider.GetRequiredService<CampaignService>();
        var due = await serviceProvider.GetRequiredService<ICampaignStore>().ListDueAsync(serviceProvider.GetRequiredService<IClock>().UtcNow, cancellationToken);

        // The site's address holds the tenant's prefix, so the route's path goes after it as is.
        string UnsubscribeUrl(string token)
            => baseUrl.TrimEnd('/') + linkGenerator.GetPathByRouteValues(MarketingConstants.UnsubscribeRouteName, new { token });

        foreach (var campaign in due)
        {
            for (var batch = 0; batch < BatchesPerRun && await campaigns.SendBatchAsync(campaign, UnsubscribeUrl, cancellationToken); batch++)
            {
            }
        }
    }
}
