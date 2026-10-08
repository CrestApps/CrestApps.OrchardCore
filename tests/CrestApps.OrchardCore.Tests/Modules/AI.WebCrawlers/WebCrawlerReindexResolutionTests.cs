using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Ingestion.Knowledge;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.OrchardCore.AI.WebCrawlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.WebCrawlers;

/// <summary>
/// Checks that the re-index service can be built on a tenant that has Web Crawlers but not File Sources.
/// </summary>
/// <remarks>
/// The framework registers the shared ingestion run service with the crawlers, but the per-item state store
/// and knowledge ingestion it needs come only with File Sources and Documents. Without the feature's guard the
/// container refuses to build the re-index service at all, and the hourly re-index task fails on every run.
/// </remarks>
public sealed class WebCrawlerReindexResolutionTests
{
    [Fact]
    public void WithoutFileSources_TheReindexServiceStillResolves()
    {
        using var provider = BuildFeature(withIngestion: false);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IWebCrawlerReindexService>());
    }

    [Fact]
    public void WithoutFileSources_ThereIsNoIngestionRunService()
    {
        using var provider = BuildFeature(withIngestion: false);
        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<IIngestionRunService>());
    }

    [Fact]
    public void WithIngestionAvailable_TheRunServiceIsBuilt()
    {
        // The guard must not switch ingestion off where File Sources supplies what it needs.
        using var provider = BuildFeature(withIngestion: true);
        using var scope = provider.CreateScope();

        Assert.IsType<DefaultIngestionRunService>(scope.ServiceProvider.GetService<IIngestionRunService>());
    }

    private static ServiceProvider BuildFeature(bool withIngestion)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        new Startup().ConfigureServices(services);

        // The stores read YesSql sessions a bare container has not got.
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IWebCrawlerStore>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IWebCrawlStateStore>()));
        services.AddScoped(_ => Mock.Of<IAIDataSourceStore>());
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IWebCrawlerReindexPlanner>()));

        if (withIngestion)
        {
            services.AddScoped(_ => Mock.Of<IIngestionItemStateStore>());
            services.AddScoped(_ => Mock.Of<IKnowledgeIngestionService>());
        }

        return services.BuildServiceProvider();
    }
}
