using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class AIDeploymentServiceRegistrationTests
{
    [Fact]
    public void AddAIDeploymentServices_ShouldRegisterSiteSettingsDeploymentManager()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IShellConfiguration>(new TestShellConfiguration(configuration));
        services.AddSingleton(Mock.Of<ISiteService>());
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<AIDeployment>>>());

        services.AddAICoreServices();
        services.AddAIDeploymentServices();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var source = scope.ServiceProvider.GetRequiredService<INamedSourceCatalogSource<AIDeployment>>();
        var managerRegistered = services.Any(x =>
            x.ServiceType == typeof(IAIDeploymentManager) &&
            x.ImplementationType == typeof(SiteSettingsAIDeploymentManager));
        var handlerRegistered = services.Any(x =>
            x.ServiceType == typeof(ICatalogEntryHandler<AIDeployment>) &&
            x.ImplementationType?.Name == "AIDeploymentHandler");

        Assert.Equal("ConfigurationAIDeploymentSource", source.GetType().Name);
        Assert.True(managerRegistered);
        Assert.True(handlerRegistered);
    }
}
