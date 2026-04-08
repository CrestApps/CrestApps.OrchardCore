using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Search.AzureAI;
using OrchardCore.Search.Elasticsearch;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

public sealed class ProviderAdapterRegistrationTests
{
    [Fact]
    public void AzureAIStartup_RegistersOrchardCoreFrameworkAdapters()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup>>())
            .ConfigureServices(services);

        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, AzureAISearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(ISearchIndexManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, AzureAISearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(ISearchDocumentManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, AzureAISearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(IDataSourceContentManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, AzureAISearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(IDataSourceDocumentReader));
    }

    [Fact]
    public void ElasticsearchStartup_RegistersOrchardCoreFrameworkAdapters()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup>>())
            .ConfigureServices(services);

        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, ElasticsearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(ISearchIndexManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, ElasticsearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(ISearchDocumentManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, ElasticsearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(IDataSourceContentManager));
        Assert.Contains(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, ElasticsearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(IDataSourceDocumentReader));
    }
}
