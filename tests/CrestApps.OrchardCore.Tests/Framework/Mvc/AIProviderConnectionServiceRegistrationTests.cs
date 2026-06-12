using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class AIProviderConnectionServiceRegistrationTests
{
    [Fact]
    public void AddAICoreServices_ShouldRegisterConfigurationBackedConnectionSource()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IShellConfiguration>(new TestShellConfiguration(configuration));
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<AIProviderConnection>>>());
        services.AddSingleton(Mock.Of<IDocumentManager<DictionaryDocument<AIProfile>>>());

        services.AddAICoreServices();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var source = scope.ServiceProvider.GetRequiredService<INamedSourceCatalogSource<AIProviderConnection>>();

        Assert.Equal("ConfigurationAIProviderConnectionSource", source.GetType().Name);
    }
}
