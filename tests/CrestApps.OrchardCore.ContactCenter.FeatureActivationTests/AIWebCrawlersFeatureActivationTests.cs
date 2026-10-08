using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.FileSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// The Web Crawlers feature on a real tenant, with and without File Sources.
/// </summary>
/// <remarks>
/// The re-index service takes the shared ingestion run service, whose per-item state store and knowledge
/// ingestion used to come only with File Sources. On a tenant with Web Crawlers alone the container refused to
/// build it and the hourly re-index task failed on every run. The item-state table is shared with File Sources,
/// so enabling both has to leave one table and a clean migration whichever feature creates it first.
/// </remarks>
public sealed class AIWebCrawlersFeatureActivationTests
{
    private const string WebCrawlers = "CrestApps.OrchardCore.AI.WebCrawlers";
    private const string FileSources = "CrestApps.OrchardCore.AI.FileSources";

    [Fact]
    public async Task WithoutFileSources_TheReindexRuns_AndTheItemStateTableExists()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("web-crawlers-alone", WebCrawlers));

        await AssertReindexRunsAndItemStateIsQueryableAsync(host, tenant);
    }

    [Fact]
    public async Task WithFileSources_TheReindexRuns_AndTheItemStateTableExists()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("web-crawlers-and-file-sources", WebCrawlers, FileSources));

        await AssertReindexRunsAndItemStateIsQueryableAsync(host, tenant);
    }

    private static async Task AssertReindexRunsAndItemStateIsQueryableAsync(
        ContactCenterFeatureActivationHost host,
        ContactCenterTenant tenant)
    {
        var count = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            // What the hourly background task does; it threw while resolving this service.
            await services.GetRequiredService<IWebCrawlerReindexService>().ReindexDueAsync(TestContext.Current.CancellationToken);

            // Throws if the migration did not create the table.
            var collection = services.GetRequiredService<IOptions<YesSqlStoreOptions>>().Value.AICollectionName;

            return await services.GetRequiredService<ISession>()
                .Query<IngestionItemState, IngestionItemStateIndex>(collection: collection)
                .CountAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal(0, count);
    }

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = features,
        };
}
