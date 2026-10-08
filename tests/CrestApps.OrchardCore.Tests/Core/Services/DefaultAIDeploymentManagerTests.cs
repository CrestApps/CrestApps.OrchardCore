using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class DefaultAIDeploymentManagerTests
{
    private readonly Mock<IAIDeploymentStore> _storeMock;
    private readonly Mock<ISiteService> _siteServiceMock;
    private readonly Mock<ISite> _siteMock;
    private readonly DefaultAIDeploymentSettings _settings;
    private readonly SiteSettingsAIDeploymentManager _manager;

    public DefaultAIDeploymentManagerTests()
    {
        _storeMock = new Mock<IAIDeploymentStore>();
        _siteServiceMock = new Mock<ISiteService>();
        _siteMock = new Mock<ISite>();
        _settings = new DefaultAIDeploymentSettings();

        _siteMock.Setup(s => s.GetOrCreate<DefaultAIDeploymentSettings>())
            .Returns(_settings);

        _siteServiceMock.Setup(s => s.GetSiteSettingsAsync())
            .ReturnsAsync(_siteMock.Object);

        _manager = new SiteSettingsAIDeploymentManager(
            _storeMock.Object,
            [],
            _siteServiceMock.Object,
            NullLogger<SiteSettingsAIDeploymentManager>.Instance,
            Options.Create(AIDeploymentSlotOptions.CreateDefault()));
    }

    [Fact]
    public async Task FindByIdAsync_WithValidId_ReturnsDeployment()
    {
        var deployment = CreateDeployment("dep-1", "gpt-4", AIDeploymentFeatureNames.TextGeneration);

        _storeMock.Setup(m => m.FindByIdAsync("dep-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var result = await _manager.FindByIdAsync("dep-1", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-1", result.ItemId);
        Assert.Equal("gpt-4", result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FindByIdAsync_WithNullOrEmptyId_Throws(string id)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(async () => await _manager.FindByIdAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindByIdAsync_WithInvalidId_ReturnsNull()
    {
        _storeMock.Setup(m => m.FindByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);

        var result = await _manager.FindByIdAsync("nonexistent", TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithExplicitDeploymentId_ReturnsThatDeployment()
    {
        var deployment = CreateDeployment("dep-explicit", "openai-chat", AIDeploymentFeatureNames.TextGeneration, modelName: "gpt-4");

        _storeMock.Setup(m => m.FindByIdAsync("dep-explicit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var connectionDeployments = new[]
        {
            CreateDeployment("dep-conn", "gpt-4o", AIDeploymentFeatureNames.TextGeneration, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionDeployments);

        var result = await _manager.ResolveSlotAsync(
            AIDeploymentSlotNames.Chat,
            deploymentName: "dep-explicit",
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-explicit", result.ItemId);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithExplicitDeploymentName_ReturnsThatDeployment()
    {
        var deployment = CreateDeployment("dep-explicit", "azure-chat", AIDeploymentFeatureNames.TextGeneration, modelName: "gpt-4.1");

        _storeMock.Setup(m => m.FindByIdAsync("azure-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("azure-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var result = await _manager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, deploymentName: "azure-chat", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-explicit", result.ItemId);
        Assert.Equal("azure-chat", result.Name);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithNoExplicit_FallsBackToGlobalDefaultBeforeScopedDeployments()
    {
        _settings.DefaultUtilityDeploymentName = "global-utility";

        var deployments = new[]
        {
            CreateDeployment("dep-scoped", "scoped-utility", AIDeploymentFeatureNames.TextGeneration, isDefault: true, connectionName: "conn-1", modelName: "gpt-4o"),
        };

        var globalDeployment = CreateDeployment("dep-global", "global-utility", AIDeploymentFeatureNames.TextGeneration, modelName: "gpt-4.1-mini");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployments);
        _storeMock.Setup(m => m.FindByIdAsync("global-utility", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("global-utility", It.IsAny<CancellationToken>()))
            .ReturnsAsync(globalDeployment);

        var result = await _manager.ResolveSlotAsync(
            AIDeploymentSlotNames.Utility,
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-global", result.ItemId);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithNoGlobalDefault_FallsBackToFirstMatchingScopedDeployment()
    {
        var scopedDeployment = CreateDeployment("dep-scoped", "gpt-4-turbo", AIDeploymentFeatureNames.TextGeneration, connectionName: "conn-1");
        var otherDeployment = CreateDeployment("dep-other", "gpt-4o", AIDeploymentFeatureNames.TextGeneration, clientName: "azure", connectionName: "conn-2");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherDeployment, scopedDeployment]);

        var result = await _manager.ResolveSlotAsync(
            AIDeploymentSlotNames.Utility,
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-scoped", result.ItemId);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithMissingGlobalDefault_FallsBackToFirstMatchingDeployment()
    {
        _settings.DefaultChatDeploymentName = "missing-chat";

        var chatDeployment = CreateDeployment("dep-chat-first", "openai-chat", AIDeploymentFeatureNames.TextGeneration, modelName: "gpt-4.1");

        _storeMock.Setup(m => m.FindByIdAsync("missing-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("missing-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatDeployment]);

        var result = await _manager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-chat-first", result.ItemId);
    }

    [Fact]
    public async Task ResolveSlotAsync_ChatSlot_SkipsRealtimeOnlyDeployment()
    {
        // A realtime deployment serves only the realtime API and answers a text completion with an HTTP 400,
        // so the chat slot excludes it even though it is the only deployment configured.
        var realtimeDeployment = CreateDeployment("dep-realtime", "gpt-realtime", AIDeploymentFeatureNames.Realtime);

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([realtimeDeployment]);

        var result = await _manager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveUtilityOrDefaultAsync_FallsBackToChat_WhenNoUtilityFound()
    {
        // No utility deployment is configured, but a chat deployment is.
        var chatDeployment = CreateDeployment("dep-chat", "gpt-4o", AIDeploymentFeatureNames.TextGeneration, isDefault: true, connectionName: "conn-1");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chatDeployment });

        _storeMock.Setup(m => m.FindByIdAsync("dep-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatDeployment);

        var result = await _manager.ResolveUtilityOrDefaultAsync(clientName: "openai");

        Assert.NotNull(result);
        Assert.Equal("dep-chat", result.ItemId);
    }

    [Fact]
    public async Task ResolveSlotAsync_UtilityWithoutFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _manager.ResolveSlotAsync(AIDeploymentSlotNames.Utility, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveSlotAsync_WithNoFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _manager.ResolveSlotAsync(AIDeploymentSlotNames.Chat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveSlotOrThrowAsync_WithNoFallbacks_ThrowsInvalidOperationException()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var manager = (IAIDeploymentManager)_manager;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.ResolveSlotOrThrowAsync(AIDeploymentSlotNames.Chat));
    }

    [Fact]
    public async Task GetAllBySlotAsync_ReturnsOnlyDeploymentsCapableOfTheSlot()
    {
        var allDeployments = new[]
        {
            CreateDeployment("dep-chat-1", "gpt-4", AIDeploymentFeatureNames.TextGeneration, clientName: "openai"),
            CreateDeployment("dep-chat-2", "gpt-4o", AIDeploymentFeatureNames.TextGeneration, clientName: "azure"),
            CreateDeployment("dep-chat-utility", "gpt-4.1-mini", AIDeploymentFeatureNames.TextGeneration, clientName: "openai"),
            CreateDeployment("dep-embed-1", "ada-002", AIDeploymentFeatureNames.TextEmbedding, clientName: "openai"),
            CreateDeployment("dep-img-1", "dall-e-3", AIDeploymentFeatureNames.ImageOutput, clientName: "openai"),
            CreateDeployment("dep-realtime-1", "gpt-realtime", AIDeploymentFeatureNames.Realtime, clientName: "openai"),
        };

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allDeployments);

        var result = (await _manager.GetAllBySlotAsync(AIDeploymentSlotNames.Chat, cancellationToken: TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, d => d.ItemId == "dep-chat-1");
        Assert.Contains(result, d => d.ItemId == "dep-chat-2");
        Assert.Contains(result, d => d.ItemId == "dep-chat-utility");
    }

    [Fact]
    public async Task GetConversationalDeploymentsAsync_ReturnsTextCapableAndRealtimeDeployments()
    {
        // The picker asks "what can this profile talk to", so it unions the chat and realtime slots. The chat
        // slot alone would drop the realtime deployment, which converses perfectly well -- it just speaks.
        var allDeployments = new[]
        {
            CreateDeployment("dep-chat-1", "gpt-4", AIDeploymentFeatureNames.TextGeneration, clientName: "openai"),
            CreateDeployment("dep-embed-1", "ada-002", AIDeploymentFeatureNames.TextEmbedding, clientName: "openai"),
            CreateDeployment("dep-realtime-1", "gpt-realtime", AIDeploymentFeatureNames.Realtime, clientName: "openai"),
        };

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allDeployments);

        var result = (await _manager.GetConversationalDeploymentsAsync(cancellationToken: TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.ItemId == "dep-chat-1");
        Assert.Contains(result, d => d.ItemId == "dep-realtime-1");
    }

    private static AIDeployment CreateDeployment(
        string itemId,
        string name,
        string feature,
        bool isDefault = false,
        string clientName = "openai",
        string connectionName = "default",
        string modelName = null)
    {
        var deployment = new AIDeployment
        {
            ItemId = itemId,
            Name = name,
            ModelName = modelName,
            Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsDefault"] = isDefault,
            },
            ClientName = clientName,
            ConnectionName = connectionName,
        };

        deployment.Put(new AIDeploymentMetadata
        {
            Features = [feature],
        });

        return deployment;
    }
}
