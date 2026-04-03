using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.OpenAI.Azure;
using CrestApps.AI.Services;
using CrestApps.Infrastructure;
using CrestApps.Mvc.Web.Areas.AI.Controllers;
using CrestApps.Mvc.Web.Areas.AI.Services;
using CrestApps.Mvc.Web.Areas.AI.ViewModels;
using CrestApps.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class AIProviderConnectionOptionsTests
{
    [Fact]
    public void AddCrestAppsAI_WhenTopLevelConnectionsConfigured_ShouldMergeThemIntoProviderOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Connections:0:Name"] = "config-primary",
                ["CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps:AI:Connections:0:ApiKey"] = "secret",
                ["CrestApps:AI:Connections:0:DefaultDeploymentName"] = "gpt-4.1-mini",
                ["CrestApps:AI:Connections:0:EnableLogging"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddCrestAppsAI();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderOptions>>().Value;

        Assert.True(options.Providers.ContainsKey("OpenAI"));
        var provider = options.Providers["OpenAI"];
        Assert.Contains("config-primary", provider.Connections.Keys);
        Assert.Equal("config-primary", provider.Connections["config-primary"].GetStringValue("ConnectionNameAlias", false));
        Assert.True(provider.Connections["config-primary"].GetBooleanOrFalseValue("EnableLogging"));
    }

    [Fact]
    public void AddCrestAppsAI_WhenClientNameIsMissing_ShouldIgnoreConnectionEntry()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Connections:0:Name"] = "config-primary",
                ["CrestApps:AI:Connections:0:ApiKey"] = "secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddCrestAppsAI();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIProviderOptions>>().Value;

        Assert.Empty(options.Providers);
    }

    [Fact]
    public void MvcAIProviderOptionsStore_ApplyTo_ShouldKeepConfiguredConnectionsAndAddUiConnections()
    {
        var options = new AIProviderOptions();
        options.Providers["OpenAI"] = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["config-primary"] = new(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ApiKey"] = "config-secret",
                    ["ConnectionNameAlias"] = "Config primary",
                    ["DefaultDeploymentName"] = "gpt-4.1",
                }),
            },
        };

        var store = new MvcAIProviderOptionsStore();
        store.Replace(
        [
            new AIProviderConnection
            {
                ClientName = "OpenAI",
                Name = "ui-secondary",
                Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ApiKey"] = "ui-secret",
                },
            },
        ]);

        store.ApplyTo(options);

        Assert.Contains("config-primary", options.Providers["OpenAI"].Connections.Keys);
        Assert.Contains("ui-secondary", options.Providers["OpenAI"].Connections.Keys);
        Assert.Equal("Config primary", options.Providers["OpenAI"].Connections["config-primary"].GetStringValue("ConnectionNameAlias", false));
        Assert.Equal("ui-secondary", options.Providers["OpenAI"].Connections["ui-secondary"].GetStringValue("ConnectionNameAlias", false));
    }

    [Fact]
    public async Task AIDeploymentController_Create_ShouldPopulateConnectionsFromMergedProviderOptions()
    {
        var deploymentCatalog = new Mock<ICatalog<AIDeployment>>();
        var providerOptions = new AIProviderOptions();
        providerOptions.Providers["OpenAI"] = new AIProvider
        {
            Connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["config-primary"] = new(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConnectionNameAlias"] = "Config primary",
                }),
                ["ui-secondary"] = new(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConnectionNameAlias"] = "UI secondary",
                }),
            },
        };

        var controller = new AIDeploymentController(
            deploymentCatalog.Object,
            new TestOptionsSnapshot<AIProviderOptions>(providerOptions));

        var result = await controller.Create();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AIDeploymentViewModel>(viewResult.Model);

        Assert.Contains(model.Connections, connection => connection.Value == "config-primary" && connection.Text == "Config primary (OpenAI)");
        Assert.Contains(model.Connections, connection => connection.Value == "ui-secondary" && connection.Text == "UI secondary (OpenAI)");
    }

    [Fact]
    public async Task AIConnectionController_Index_ShouldIncludeConfiguredConnectionsAsReadOnly()
    {
        var connectionCatalog = new Mock<ICatalog<AIProviderConnection>>();
        connectionCatalog.Setup(catalog => catalog.GetAllAsync()).ReturnsAsync(
        [
            new AIProviderConnection
            {
                ItemId = "ui-connection",
                Name = "ui-secondary",
                DisplayText = "UI secondary",
                Source = "OpenAI",
            },
        ]);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:Connections:0:Name"] = "config-primary",
                ["CrestApps:AI:Connections:0:ClientName"] = "OpenAI",
                ["CrestApps:AI:Connections:0:ConnectionNameAlias"] = "Config primary",
            })
            .Build();

        var controller = new AIConnectionController(
            connectionCatalog.Object,
            configuration,
            new MvcAIProviderOptionsStore(),
            new OptionsCache<AIProviderOptions>());

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyCollection<AIConnectionViewModel>>(viewResult.Model);

        Assert.Contains(model, connection => connection.Name == "config-primary" && connection.IsReadOnly);
        Assert.Contains(model, connection => connection.Name == "ui-secondary" && !connection.IsReadOnly);
    }

    [Fact]
    public async Task AIDeploymentController_Index_ShouldMarkConfiguredDeploymentsAsReadOnly()
    {
        var deploymentCatalog = new Mock<ICatalog<AIDeployment>>();
        deploymentCatalog.Setup(catalog => catalog.GetAllAsync()).ReturnsAsync(
        [
            new AIDeployment
            {
                ItemId = AIConfigurationRecordIds.CreateDeploymentId("AzureSpeech", null, "whisper"),
                Name = "whisper",
                ModelName = "whisper",
                ClientName = "AzureSpeech",
                Type = AIDeploymentType.SpeechToText,
            },
        ]);

        var controller = new AIDeploymentController(
            deploymentCatalog.Object,
            new TestOptionsSnapshot<AIProviderOptions>(new AIProviderOptions()));

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyCollection<AIDeploymentViewModel>>(viewResult.Model);

        Assert.Contains(model, deployment => deployment.TechnicalName == "whisper" && deployment.IsReadOnly);
    }

    [Fact]
    public void AddAzureOpenAIProvider_ShouldRegisterAzureSpeechAsDeploymentProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCrestAppsAI();
        services.AddAzureOpenAIProvider();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<AIOptions>>().Value;

        Assert.True(options.Deployments.ContainsKey(AzureOpenAIConstants.AzureSpeechProviderName));
        Assert.True(options.Deployments[AzureOpenAIConstants.AzureSpeechProviderName].SupportsContainedConnection);
    }

    private sealed class TestOptionsSnapshot<TOptions> : IOptionsSnapshot<TOptions>
        where TOptions : class
    {
        public TestOptionsSnapshot(TOptions value) => Value = value;

        public TOptions Value { get; }

        public TOptions Get(string name) => Value;
    }
}
