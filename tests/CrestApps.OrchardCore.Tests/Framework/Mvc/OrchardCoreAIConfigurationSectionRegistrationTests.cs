using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class OrchardCoreAIConfigurationSectionRegistrationTests
{
    [Fact]
    public void AddAIDeploymentServices_ShouldRegisterCorrectDeploymentSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddAICoreServices();

        services.AddAIDeploymentServices();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIDeploymentCatalogOptions>>().Value;

        Assert.Contains("CrestApps:AI:Deployments", options.DeploymentSections);
    }

    [Fact]
    public void Startup_ShouldRegisterCorrectConnectionAndProviderSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderConnectionCatalogOptions>>().Value;

        Assert.Contains("CrestApps:AI:Connections", options.ConnectionSections);
        Assert.Contains("CrestApps_AI:Providers", options.ProviderSections);
        Assert.Contains("CrestApps:AI:Providers", options.ProviderSections);
    }

    [Fact]
    public void Startup_ShouldRegisterConfigurationBackedConnectionsSource()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Connections:0:Name"] = "primary-openai",
                ["CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps_AI:Connections:0:Name"] = "legacy-openai",
                ["CrestApps_AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:Endpoint"] = "https://example.openai.azure.com/",
                ["CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:AuthenticationType"] = "ApiKey",
                ["CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:ApiKey"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddSingleton<IShellConfiguration>(new MockShellConfiguration(configuration));
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(CreateDocumentManager<AIProviderConnection>());
        services.AddSingleton(CreateDocumentManager<AIProfile>());
        services.AddSingleton(Mock.Of<ISession>());
        services.AddSingleton(Mock.Of<IClock>());

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var source = scope.ServiceProvider.GetRequiredService<INamedSourceCatalogSource<AIProviderConnection>>();
        var sources = scope.ServiceProvider.GetServices<INamedSourceCatalogSource<AIProviderConnection>>();

        Assert.NotNull(source);
        Assert.Contains(sources, candidate => candidate.GetType().Name == "ConfigurationAIProviderConnectionSource");
    }

    private static IDocumentManager<DictionaryDocument<T>> CreateDocumentManager<T>()
        where T : class
    {
        var manager = new Mock<IDocumentManager<DictionaryDocument<T>>>();
        manager
            .Setup(m => m.GetOrCreateImmutableAsync())
            .ReturnsAsync(new DictionaryDocument<T>());

        return manager.Object;
    }

    /// <summary>
    /// A minimal IShellConfiguration wrapper around an IConfiguration for testing.
    /// In production, IShellConfiguration is scoped to the OrchardCore: section
    /// and includes App_Data/appsettings.json. In tests, we use the config directly.
    /// </summary>
    private sealed class MockShellConfiguration : IShellConfiguration
    {
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public MockShellConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
            => _configuration.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
            => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key)
            => _configuration.GetSection(key);
    }
}
