using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
            NullLogger<SiteSettingsAIDeploymentManager>.Instance);
    }

    [Fact]
    public async Task FindByIdAsync_WithValidId_ReturnsDeployment()
    {
        var deployment = CreateDeployment("dep-1", "gpt-4", AIDeploymentPurpose.Chat);

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

    public async Task ResolveAsync_WithExplicitDeploymentId_ReturnsThatDeployment()
    {
        var deployment = CreateDeployment("dep-explicit", "openai-chat", AIDeploymentPurpose.Chat, modelName: "gpt-4");

        _storeMock.Setup(m => m.FindByIdAsync("dep-explicit", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var connectionDeployments = new[]
        {
            CreateDeployment("dep-conn", "gpt-4o", AIDeploymentPurpose.Chat, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionDeployments);

        var result = await _manager.ResolveOrDefaultAsync(
            AIDeploymentPurpose.Chat,
            deploymentName: "dep-explicit",
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-explicit", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitDeploymentName_ReturnsThatDeployment()
    {
        var deployment = CreateDeployment("dep-explicit", "azure-chat", AIDeploymentPurpose.Chat, modelName: "gpt-4.1");

        _storeMock.Setup(m => m.FindByIdAsync("azure-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("azure-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, deploymentName: "azure-chat", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-explicit", result.ItemId);
        Assert.Equal("azure-chat", result.Name);
    }

    [Fact]
    public async Task ResolveAsync_WithNoExplicit_FallsBackToGlobalDefaultBeforeScopedDeployments()
    {
        _settings.DefaultUtilityDeploymentName = "global-utility";

        var deployments = new[]
        {
            CreateDeployment("dep-scoped", "scoped-utility", AIDeploymentPurpose.Utility, isDefault: true, connectionName: "conn-1", modelName: "gpt-4o"),
        };

        var globalDeployment = CreateDeployment("dep-global", "global-utility", AIDeploymentPurpose.Utility, modelName: "gpt-4.1-mini");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployments);
        _storeMock.Setup(m => m.FindByIdAsync("global-utility", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("global-utility", It.IsAny<CancellationToken>()))
            .ReturnsAsync(globalDeployment);

        var result = await _manager.ResolveOrDefaultAsync(
            AIDeploymentPurpose.Utility,
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-global", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_WithNoGlobalDefault_FallsBackToFirstMatchingScopedDeployment()
    {
        var scopedDeployment = CreateDeployment("dep-scoped", "gpt-4-turbo", AIDeploymentPurpose.Utility, connectionName: "conn-1");
        var otherDeployment = CreateDeployment("dep-other", "gpt-4o", AIDeploymentPurpose.Utility, clientName: "azure", connectionName: "conn-2");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherDeployment, scopedDeployment]);

        var result = await _manager.ResolveOrDefaultAsync(
            AIDeploymentPurpose.Utility,
            clientName: "openai",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-scoped", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_WithMissingGlobalDefault_FallsBackToFirstMatchingDeployment()
    {
        _settings.DefaultChatDeploymentName = "missing-chat";

        var chatDeployment = CreateDeployment("dep-chat-first", "openai-chat", AIDeploymentPurpose.Chat, modelName: "gpt-4.1");

        _storeMock.Setup(m => m.FindByIdAsync("missing-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.FindByNameAsync("missing-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AIDeployment)null);
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatDeployment]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("dep-chat-first", result.ItemId);
    }

    [Fact]
    public async Task ResolveUtilityOrDefaultAsync_FallsBackToChat_WhenNoUtilityFound()
    {
        // No utility deployment exists, but a chat deployment does.
        var chatDeployment = CreateDeployment("dep-chat", "gpt-4o", AIDeploymentPurpose.Chat, isDefault: true, connectionName: "conn-1");

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chatDeployment });

        _storeMock.Setup(m => m.FindByIdAsync("dep-chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatDeployment);

        var result = await _manager.ResolveUtilityOrDefaultAsync(clientName: "openai");

        Assert.NotNull(result);
        Assert.Equal("dep-chat", result.ItemId);
        Assert.Equal(AIDeploymentPurpose.Chat, result.Purpose);
    }

    [Fact]
    public async Task ResolveOrDefaultAsync_UtilityWithoutFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentPurpose.Utility, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithNoFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentPurpose.Chat, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithNoFallbacks_ThrowsInvalidOperationException()
    {
        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var manager = (IAIDeploymentManager)_manager;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.ResolveAsync(AIDeploymentPurpose.Chat));
    }

    [Fact]
    public async Task GetAllByTypeAsync_ReturnsOnlyMatchingType()
    {
        var allDeployments = new[]
        {
            CreateDeployment("dep-chat-1", "gpt-4", AIDeploymentPurpose.Chat, clientName: "openai"),
            CreateDeployment("dep-chat-2", "gpt-4o", AIDeploymentPurpose.Chat, clientName: "azure"),
            CreateDeployment("dep-chat-utility", "gpt-4.1-mini", AIDeploymentPurpose.Chat | AIDeploymentPurpose.Utility, clientName: "openai"),
            CreateDeployment("dep-embed-1", "ada-002", AIDeploymentPurpose.Embedding, clientName: "openai"),
            CreateDeployment("dep-img-1", "dall-e-3", AIDeploymentPurpose.Image, clientName: "openai"),
        };

        _storeMock.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allDeployments);

        var result = (await _manager.GetAllByPurposeAsync(AIDeploymentPurpose.Chat, cancellationToken: TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, d => Assert.True(d.SupportsPurpose(AIDeploymentPurpose.Chat)));
        Assert.Contains(result, d => d.ItemId == "dep-chat-1");
        Assert.Contains(result, d => d.ItemId == "dep-chat-2");
        Assert.Contains(result, d => d.ItemId == "dep-chat-utility");
    }

    private static AIDeployment CreateDeployment(
        string itemId,
        string name,
        AIDeploymentPurpose type,
        bool isDefault = false,
        string clientName = "openai",
        string connectionName = "default",
        string modelName = null)
    {
        return new AIDeployment
        {
            ItemId = itemId,
            Name = name,
            ModelName = modelName,
            Purpose = type,
            Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsDefault"] = isDefault,
            },
            ClientName = clientName,
            ConnectionName = connectionName,
        };
    }
}
