using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Configuration;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class OrchardCoreAIConfigurationSectionRegistrationTests
{
    [Fact]
    public void AddAIDeploymentServices_ShouldRegisterCorrectAndLegacyDeploymentSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        services.AddAIDeploymentServices();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIDeploymentCatalogOptions>>().Value;

        Assert.Contains("OrchardCore:CrestApps:AI:Deployments", options.DeploymentSections);
        Assert.Contains("OrcahrdCore:CrestApps:AI:Deployments", options.DeploymentSections);
    }

    [Fact]
    public void Startup_ShouldRegisterCorrectAndLegacyConnectionAndProviderSections()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderConnectionCatalogOptions>>().Value;

        Assert.Contains("OrchardCore:CrestApps:AI:Connections", options.ConnectionSections);
        Assert.Contains("OrcahrdCore:CrestApps:AI:Connections", options.ConnectionSections);
        Assert.Contains("OrchardCore:CrestApps:AI:Providers", options.ProviderSections);
        Assert.Contains("OrcahrdCore:CrestApps:AI:Providers", options.ProviderSections);
    }

    [Fact]
    public void Startup_ShouldMergeOrchardCoreConnectionSectionsIntoProviderOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["OrchardCore:CrestApps:AI:Connections:0:Name"] = "primary-openai",
                ["OrchardCore:CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["OrcahrdCore:CrestApps:AI:Connections:0:Name"] = "legacy-openai",
                ["OrcahrdCore:CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["OrchardCore:CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:Endpoint"] = "https://example.openai.azure.com/",
                ["OrchardCore:CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:AuthenticationType"] = "ApiKey",
                ["OrchardCore:CrestApps:AI:Providers:AzureOpenAI:Connections:azure-shared:ApiKey"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddOptions();

        new Startup().ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderOptions>>().Value;

        Assert.Contains("primary-openai", options.Providers["OpenAI"].Connections.Keys);
        Assert.Contains("legacy-openai", options.Providers["OpenAI"].Connections.Keys);
        Assert.Contains("azure-shared", options.Providers["Azure"].Connections.Keys);
    }
}
