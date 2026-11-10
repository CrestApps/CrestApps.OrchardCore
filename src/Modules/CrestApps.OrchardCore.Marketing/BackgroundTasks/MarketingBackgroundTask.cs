using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Marketing.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketing.BackgroundTasks;

/// <summary>
/// Every minute: counts a batch of the orders paid before Marketing was turned on into customers' purchases, and hands
/// each campaign that's sending or whose time has come to the outreach engine, which sends it in batches.
/// </summary>
[BackgroundTask(
    Title = "Marketing",
    Schedule = "* * * * *",
    Description = "Builds customers' purchases from earlier orders, and starts sending campaigns through the outreach engine.",
    LockTimeout = 3_000,
    LockExpiration = 600_000,
    UsePipeline = true)]
public sealed class MarketingBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => BackfillAndSendAsync(serviceProvider, run, cancellationToken));

    private static async Task BackfillAndSendAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        await serviceProvider.GetRequiredService<CustomerPurchasesService>().BackfillAsync(cancellationToken);

        var baseUrl = (await serviceProvider.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).BaseUrl;

        // Without the site's address there's no unsubscribe link, so nothing goes out.
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            run.Skip();

            return;
        }

        var campaigns = serviceProvider.GetRequiredService<CampaignService>();
        var due = await serviceProvider.GetRequiredService<ICampaignStore>().ListDueAsync(serviceProvider.GetRequiredService<IClock>().UtcNow, cancellationToken);

        foreach (var campaign in due)
        {
            run.AddItems(1, await campaigns.StartSendingAsync(campaign, cancellationToken) ? "started" : "not_started");
        }
    }
}
