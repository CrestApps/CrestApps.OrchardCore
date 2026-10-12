using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.AzureAI;
using OrchardCore.Elasticsearch;

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

    /// <summary>
    /// The knowledge base search resolves the filter translator by the index provider name. Without one, the
    /// filter of an AI profile or chat interaction is ignored, so every search returns rows the filter was
    /// written to keep out.
    /// </summary>
    [Fact]
    public void ElasticsearchStartup_RegistersTheODataFilterTranslator()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Startup>>())
            .ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var translator = serviceProvider.GetKeyedService<IODataFilterTranslator>(ElasticsearchConstants.ProviderName);

        Assert.NotNull(translator);
        Assert.Equal("""{"term":{"filters.category":"docs"}}""", translator.Translate("category eq 'docs'"));
    }

    /// <summary>
    /// The Azure AI Search knowledge base index declares no <c>filters</c> field and no typed knowledge
    /// columns, and Azure AI Search rejects a filter on an undeclared field. A translator would turn every
    /// filtered search, and the picture search that runs on every preemptive turn, into a failed request. Until
    /// the index declares those fields, the filter is ignored with a warning instead.
    /// </summary>
    [Fact]
    public void AzureAIStartup_RegistersNoODataFilterTranslator()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup(Mock.Of<IStringLocalizer<CrestApps.OrchardCore.AI.DataSources.AzureAI.Startup>>())
            .ConfigureServices(services);

        Assert.DoesNotContain(services, descriptor =>
            descriptor.IsKeyedService &&
            Equals(descriptor.ServiceKey, AzureAISearchConstants.ProviderName) &&
            descriptor.ServiceType == typeof(IODataFilterTranslator));
    }
}
