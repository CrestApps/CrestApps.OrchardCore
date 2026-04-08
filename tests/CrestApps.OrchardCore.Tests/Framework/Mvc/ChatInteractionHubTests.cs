using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Handlers;
using CrestApps.Core.AI.Chat.Hubs;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Hubs;
using CrestApps.Core.Mvc.Web.Areas.ChatInteractions.Models;
using CrestApps.Core.Mvc.Web.Services;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class ChatInteractionHubTests
{
    [Fact]
    public async Task SaveSettings_PersistsCoreAndTemplateSettings()
    {
        var interaction = new ChatInteraction
        {
            ItemId = "chat-1",
            Title = "Original title",
        };

        var managerMock = new Mock<ICatalogManager<ChatInteraction>>();
        managerMock
            .Setup(manager => manager.FindByIdAsync(interaction.ItemId))
            .Returns(new ValueTask<ChatInteraction>(interaction));
        managerMock
            .Setup(manager => manager.UpdateAsync(interaction, null))
            .Returns(ValueTask.CompletedTask);

        var sessionMock = new Mock<ISession>();
        sessionMock
            .Setup(session => session.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var callerMock = new Mock<IChatInteractionHubClient>();
        callerMock
            .Setup(client => client.SettingsSaved(interaction.ItemId, "Updated title"))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubCallerClients<IChatInteractionHubClient>>();
        clientsMock
            .SetupGet(clients => clients.Caller)
            .Returns(callerMock.Object);

        var hub = new ChatInteractionHub(
            managerMock.Object,
            new Mock<IChatInteractionPromptStore>(MockBehavior.Strict).Object,
            new Mock<IOrchestrationContextBuilder>(MockBehavior.Strict).Object,
            new Mock<IOrchestratorResolver>(MockBehavior.Strict).Object,
            [new PromptTemplateChatInteractionSettingsHandler()],
            TimeProvider.System,
            CreateCitationCollector(),
            sessionMock.Object,
            new Mock<IAIDeploymentManager>(MockBehavior.Strict).Object,
            new Mock<IAIClientFactory>(MockBehavior.Strict).Object,
            CreateSettingsService<ChatInteractionSettings>(AppDataConfigurationSections.ChatInteraction),
            CreateSettingsService<DefaultAIDeploymentSettings>(AppDataConfigurationSections.DefaultDeployments),
            new Mock<IServiceScopeFactory>(MockBehavior.Strict).Object,
            NullLogger<ChatInteractionHub>.Instance)
        {
            Clients = clientsMock.Object,
        };

        using var json = JsonDocument.Parse("""
            {
              "title":"Updated title",
              "agentNames":["agent-a","agent-b"],
              "promptTemplates":[
                {
                  "templateId":"template-1",
                  "promptParameters":"{\"topic\":\"embeddings\"}"
                }
              ]
            }
            """);

        await hub.SaveSettings(interaction.ItemId, json.RootElement.Clone());

        Assert.Equal("Updated title", interaction.Title);
        Assert.Equal(["agent-a", "agent-b"], interaction.AgentNames);

        var promptTemplateMetadata = interaction.As<PromptTemplateMetadata>();
        var template = Assert.Single(promptTemplateMetadata.Templates);
        Assert.Equal("template-1", template.TemplateId);
        Assert.NotNull(template.Parameters);
        Assert.Equal("embeddings", Assert.IsType<string>(template.Parameters["topic"]));

        managerMock.Verify(manager => manager.FindByIdAsync(interaction.ItemId), Times.Once);
        managerMock.Verify(manager => manager.UpdateAsync(interaction, null), Times.Once);
        sessionMock.Verify(session => session.SaveChangesAsync(), Times.Once);
        callerMock.Verify(client => client.SettingsSaved(interaction.ItemId, "Updated title"), Times.Once);
    }

    [Fact]
    public async Task SaveSettings_WithDataSourceSettings_PersistsRagMetadata()
    {
        var interaction = new ChatInteraction
        {
            ItemId = "chat-2",
            Title = "Knowledge chat",
        };

        var managerMock = new Mock<ICatalogManager<ChatInteraction>>();
        managerMock
            .Setup(manager => manager.FindByIdAsync(interaction.ItemId))
            .Returns(new ValueTask<ChatInteraction>(interaction));
        managerMock
            .Setup(manager => manager.UpdateAsync(interaction, null))
            .Returns(ValueTask.CompletedTask);

        var dataSourceCatalog = new Mock<ICatalog<AIDataSource>>();
        dataSourceCatalog
            .Setup(catalog => catalog.FindByIdAsync("datasource-1"))
            .ReturnsAsync(new AIDataSource { ItemId = "datasource-1" });

        var serviceProvider = new ServiceCollection()
            .AddSingleton(dataSourceCatalog.Object)
            .BuildServiceProvider();

        var sessionMock = new Mock<ISession>();
        sessionMock
            .Setup(session => session.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var callerMock = new Mock<IChatInteractionHubClient>();
        callerMock
            .Setup(client => client.SettingsSaved(interaction.ItemId, interaction.Title))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubCallerClients<IChatInteractionHubClient>>();
        clientsMock
            .SetupGet(clients => clients.Caller)
            .Returns(callerMock.Object);

        var hub = new ChatInteractionHub(
            managerMock.Object,
            new Mock<IChatInteractionPromptStore>(MockBehavior.Strict).Object,
            new Mock<IOrchestrationContextBuilder>(MockBehavior.Strict).Object,
            new Mock<IOrchestratorResolver>(MockBehavior.Strict).Object,
            [new DataSourceChatInteractionSettingsHandler(serviceProvider, NullLogger<DataSourceChatInteractionSettingsHandler>.Instance)],
            TimeProvider.System,
            CreateCitationCollector(),
            sessionMock.Object,
            new Mock<IAIDeploymentManager>(MockBehavior.Strict).Object,
            new Mock<IAIClientFactory>(MockBehavior.Strict).Object,
            CreateSettingsService<ChatInteractionSettings>(AppDataConfigurationSections.ChatInteraction),
            CreateSettingsService<DefaultAIDeploymentSettings>(AppDataConfigurationSections.DefaultDeployments),
            new Mock<IServiceScopeFactory>(MockBehavior.Strict).Object,
            NullLogger<ChatInteractionHub>.Instance)
        {
            Clients = clientsMock.Object,
        };

        using var json = JsonDocument.Parse("""
            {
              "dataSourceId":"datasource-1",
              "strictness":4,
              "topNDocuments":7,
              "isInScope":false,
              "filter":"category eq 'docs'"
            }
            """);

        await hub.SaveSettings(interaction.ItemId, json.RootElement.Clone());

        var dataSourceMetadata = interaction.As<DataSourceMetadata>();
        Assert.Equal("datasource-1", dataSourceMetadata.DataSourceId);

        var ragMetadata = interaction.As<AIDataSourceRagMetadata>();
        Assert.Equal(4, ragMetadata.Strictness);
        Assert.Equal(7, ragMetadata.TopNDocuments);
        Assert.False(ragMetadata.IsInScope);
        Assert.Equal("category eq 'docs'", ragMetadata.Filter);

        managerMock.Verify(manager => manager.FindByIdAsync(interaction.ItemId), Times.Once);
        managerMock.Verify(manager => manager.UpdateAsync(interaction, null), Times.Once);
        sessionMock.Verify(session => session.SaveChangesAsync(), Times.Once);
        callerMock.Verify(client => client.SettingsSaved(interaction.ItemId, interaction.Title), Times.Once);
    }

    private static MvcCitationReferenceCollector CreateCitationCollector()
        => new(new CompositeAIReferenceLinkResolver(new ServiceCollection().BuildServiceProvider()));

    private static AppDataSettingsService<T> CreateSettingsService<T>(string sectionKey)
        where T : new()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var appDataPath = Path.Combine(Path.GetTempPath(), "copilot-chatinteractionhubtests", Guid.NewGuid().ToString("N"));
        var configurationFileService = new AppDataConfigurationFileService(appDataPath);
        var sectionResolver = new AppDataSettingsSectionResolver([new(typeof(T), sectionKey)]);

        return new AppDataSettingsService<T>(configuration, configurationFileService, sectionResolver);
    }
}
