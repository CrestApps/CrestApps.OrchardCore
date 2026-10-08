using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.WebCrawlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.WebCrawlers;

/// <summary>
/// Checks that the re-index service can be built on a tenant that has Web Crawlers but not File Sources.
/// </summary>
/// <remarks>
/// The framework registers the shared ingestion run service with the crawlers. Its per-item state store and
/// knowledge ingestion used to come only with File Sources and AI Documents, so on a tenant with Web Crawlers
/// alone the container refused to build the re-index service and the hourly re-index task failed on every run.
/// </remarks>
public sealed class WebCrawlerReindexResolutionTests
{
    [Fact]
    public void WithoutFileSources_TheReindexServiceAndTheRunServiceResolve()
    {
        using var provider = BuildFeature();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IWebCrawlerReindexService>());
        Assert.IsType<DefaultIngestionRunService>(scope.ServiceProvider.GetService<IIngestionRunService>());
    }

    private static ServiceProvider BuildFeature()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        new Startup().ConfigureServices(services);

        // The stores read YesSql sessions a bare container has not got.
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IWebCrawlerStore>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IWebCrawlStateStore>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IIngestionItemStateStore>()));
        services.Replace(ServiceDescriptor.Scoped(_ => Mock.Of<IKnowledgeObjectStore>()));

        // What the AI Data Sources feature and Orchard Core supply to every tenant this feature runs on.
        services.AddScoped(_ => Mock.Of<IAIDataSourceStore>());
        services.AddScoped(_ => Mock.Of<IAIDataSourceIndexingQueue>());
        services.AddScoped(_ => Mock.Of<IAIDeploymentManager>());
        services.AddScoped(_ => Mock.Of<IAIClientFactory>());
        services.AddScoped(_ => Mock.Of<ITemplateService>());
        services.AddSingleton(Mock.Of<IHostEnvironment>(environment => environment.ContentRootPath == Path.GetTempPath()));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        return services.BuildServiceProvider();
    }
}
