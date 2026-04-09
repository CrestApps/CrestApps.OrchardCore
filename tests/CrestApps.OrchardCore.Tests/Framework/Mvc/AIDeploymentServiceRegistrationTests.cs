using CrestApps.Core.AI.Models;
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

        var sourceCatalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIDeployment>>();
        var namedCatalog = scope.ServiceProvider.GetRequiredService<INamedCatalog<AIDeployment>>();

        Assert.IsType<DefaultAIDeploymentStore>(sourceCatalog);
        Assert.IsNotType<DefaultAIDeploymentStore>(namedCatalog);
    }
}
