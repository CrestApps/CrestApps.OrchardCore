using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Documents.Models;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class AIProviderConnectionServiceRegistrationTests
{
    [Fact]
    public void AddAICoreServices_ShouldUseConfigurationBackedConnectionCatalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<AIProviderConnection>>>());
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<CrestApps.Core.AI.Models.AIProfile>>>());

        services.AddAICoreServices();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var sourceCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIProviderConnection>>();
        var namedCatalog = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIProviderConnection>>();
        var persistedCatalog = scope.ServiceProvider.GetRequiredKeyedService<INamedSourceCatalog<AIProviderConnection>>(ConfigurationAIProviderConnectionCatalog.PersistedCatalogKey);

        Assert.IsType<ConfigurationAIProviderConnectionCatalog>(sourceCatalog);
        Assert.IsType<ConfigurationAIProviderConnectionCatalog>(namedCatalog);
        Assert.IsType<AIProviderConnectionStore>(persistedCatalog);
    }
}
