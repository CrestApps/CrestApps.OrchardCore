using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Configuration;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Documents.Models;
using OrchardCore.Environment.Shell.Configuration;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class OrchardCoreAIConfigurationSectionRegistrationTests
{
    [Fact]
    public void AddAIDeploymentServices_ShouldRegisterCorrectDeploymentSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        services.AddAIDeploymentServices();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIDeploymentCatalogOptions>>().Value;

        Assert.Contains("CrestApps_AI:Deployments", options.DeploymentSections);
    }

    [Fact]
    public void Startup_ShouldRegisterCorrectConnectionAndProviderSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderConnectionCatalogOptions>>().Value;

        Assert.Contains("CrestApps_AI:Connections", options.ConnectionSections);
        Assert.Contains("CrestApps_AI:Providers", options.ProviderSections);
    }

    [Fact]
    public async Task Startup_ShouldExposeConfiguredConnectionsThroughTheActiveCatalog()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Connections:0:Name"] = "primary-openai",
                ["CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps_AI:Connections:0:Name"] = "legacy-openai",
                ["CrestApps_AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps_AI:Providers:AzureOpenAI:Connections:azure-shared:Endpoint"] = "https://example.openai.azure.com/",
                ["CrestApps_AI:Providers:AzureOpenAI:Connections:azure-shared:AuthenticationType"] = "ApiKey",
                ["CrestApps_AI:Providers:AzureOpenAI:Connections:azure-shared:ApiKey"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IShellConfiguration>(new MockShellConfiguration(configuration));
        services.AddLogging();
        services.AddOptions();
        services.AddSingleton(CreateDocumentManager<AIProviderConnection>());
        services.AddSingleton(CreateDocumentManager<AIProfile>());

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var catalog = scope.ServiceProvider.GetRequiredService<INamedSourceCatalog<AIProviderConnection>>();
        var connections = await catalog.GetAllAsync();

        Assert.Contains(connections, connection => connection.Name == "primary-openai" && connection.ClientName == "OpenAI");
        Assert.Contains(connections, connection => connection.Name == "legacy-openai" && connection.ClientName == "OpenAI");
        Assert.Contains(connections, connection => connection.Name == "azure-shared" && connection.ClientName == "Azure");
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
        private readonly IConfiguration _configuration;

        public MockShellConfiguration(IConfiguration configuration)
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
