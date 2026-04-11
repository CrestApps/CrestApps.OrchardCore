using CrestApps.Core.AI.Deployments;
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

public sealed class AIDeploymentServiceRegistrationTests
{
    [Fact]
    public void AddAIDeploymentServices_ShouldKeepSourceStoreSeparateFromConfigurationCatalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<AIDeployment>>>());

        services.AddAIDeploymentServices();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var deploymentStore = scope.ServiceProvider.GetRequiredService<IAIDeploymentStore>();
        var namedCatalog = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIDeployment>>();
        var persistedCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIDeployment>>();

        Assert.IsType<ConfigurationAIDeploymentCatalog>(deploymentStore);
        Assert.IsType<ConfigurationAIDeploymentCatalog>(namedCatalog);
        Assert.IsType<DefaultAIDeploymentStore>(persistedCatalog);
    }
}
