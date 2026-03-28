using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class DefaultAIDeploymentManagerTests
{
    private readonly Mock<INamedSourceCatalog<AIDeployment>> _storeMock;
    private readonly Mock<ISiteService> _siteServiceMock;
    private readonly Mock<ISite> _siteMock;
    private readonly DefaultAIDeploymentSettings _settings;
    private readonly DefaultAIDeploymentManager _manager;

    public DefaultAIDeploymentManagerTests()
    {
        _storeMock = new Mock<INamedSourceCatalog<AIDeployment>>();
        _siteServiceMock = new Mock<ISiteService>();
        _siteMock = new Mock<ISite>();
        _settings = new DefaultAIDeploymentSettings();

        _siteMock.Setup(s => s.As<DefaultAIDeploymentSettings>())
            .Returns(_settings);

        _siteServiceMock.Setup(s => s.GetSiteSettingsAsync())
            .ReturnsAsync(_siteMock.Object);

        _manager = new DefaultAIDeploymentManager(
            _storeMock.Object,
            [],
            _siteServiceMock.Object,
            NullLogger<DefaultAIDeploymentManager>.Instance);
    }

    [Fact]
    public async Task FindByIdAsync_WithValidId_ReturnsDeployment()
    {
        var deployment = CreateDeployment("dep-1", "gpt-4", AIDeploymentType.Chat);

        _storeMock.Setup(m => m.FindByIdAsync("dep-1"))
            .ReturnsAsync(deployment);

        var result = await _manager.FindByIdAsync("dep-1");

        Assert.NotNull(result);
        Assert.Equal("dep-1", result.ItemId);
        Assert.Equal("gpt-4", result.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FindByIdAsync_WithNullOrEmptyId_ReturnsNull(string id)
    {
        _storeMock.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((AIDeployment)null);

        var result = await _manager.FindByIdAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByIdAsync_WithInvalidId_ReturnsNull()
    {
        _storeMock.Setup(m => m.FindByIdAsync("nonexistent"))
            .ReturnsAsync((AIDeployment)null);

        var result = await _manager.FindByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDefaultAsync_WithType_ReturnsDefaultDeployment()
    {
        var deployments = new[]
        {
            CreateDeployment("dep-1", "gpt-4", AIDeploymentType.Chat, isDefault: false, connectionName: "conn-1"),
            CreateDeployment("dep-2", "gpt-4o", AIDeploymentType.Chat, isDefault: true, connectionName: "conn-1"),
            CreateDeployment("dep-3", "ada-002", AIDeploymentType.Embedding, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(deployments);

        var result = await _manager.GetDefaultAsync("openai", "conn-1", AIDeploymentType.Chat);

        Assert.NotNull(result);
        Assert.Equal("dep-2", result.ItemId);
        Assert.True(result.IsDefault);
    }

    [Fact]
    public async Task GetDefaultAsync_WithMultiTypeDefault_ReturnsDeployment()
    {
        var deployments = new[]
        {
            CreateDeployment("dep-multi", "gpt-4.1-mini", AIDeploymentType.Chat | AIDeploymentType.Utility, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(deployments);

        var result = await _manager.GetDefaultAsync("openai", "conn-1", AIDeploymentType.Utility);

        Assert.NotNull(result);
        Assert.Equal("dep-multi", result.ItemId);
        Assert.True(result.SupportsType(AIDeploymentType.Chat));
        Assert.True(result.SupportsType(AIDeploymentType.Utility));
    }

    [Fact]
    public async Task GetDefaultAsync_WithNoDefault_ReturnsNull()
    {
        var deployments = new[]
        {
            CreateDeployment("dep-1", "gpt-4", AIDeploymentType.Chat, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(deployments);

        var result = await _manager.GetDefaultAsync("openai", "conn-1", AIDeploymentType.Embedding);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitDeploymentId_ReturnsThatDeployment()
    {
        var deployment = CreateDeployment("dep-explicit", "gpt-4", AIDeploymentType.Chat);

        _storeMock.Setup(m => m.FindByIdAsync("dep-explicit"))
            .ReturnsAsync(deployment);

        var connectionDeployments = new[]
        {
            CreateDeployment("dep-conn", "gpt-4o", AIDeploymentType.Chat, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(connectionDeployments);

        var result = await _manager.ResolveOrDefaultAsync(
            AIDeploymentType.Chat,
            deploymentId: "dep-explicit",
            clientName: "openai",
            connectionName: "conn-1");

        Assert.NotNull(result);
        Assert.Equal("dep-explicit", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_WithNoExplicit_FallsBackToConnectionDefault()
    {
        var deployments = new[]
        {
            CreateDeployment("dep-conn-default", "gpt-4o", AIDeploymentType.Utility, isDefault: true, connectionName: "conn-1"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(deployments);

        var result = await _manager.ResolveOrDefaultAsync(
            AIDeploymentType.Utility,
            clientName: "openai",
            connectionName: "conn-1");

        Assert.NotNull(result);
        Assert.Equal("dep-conn-default", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_WithNoConnectionDefault_FallsBackToGlobalDefault()
    {
        _settings.DefaultUtilityDeploymentId = "dep-global";

        var globalDeployment = CreateDeployment("dep-global", "gpt-4-turbo", AIDeploymentType.Utility);

        _storeMock.Setup(m => m.FindByIdAsync("dep-global"))
            .ReturnsAsync(globalDeployment);

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentType.Utility);

        Assert.NotNull(result);
        Assert.Equal("dep-global", result.ItemId);
    }

    [Fact]
    public async Task ResolveAsync_ChatWithNoConnectionDefault_FallsBackToGlobalChatDefault()
    {
        _settings.DefaultChatDeploymentId = "dep-chat-global";

        var globalDeployment = CreateDeployment("dep-chat-global", "gpt-4.1", AIDeploymentType.Chat);

        _storeMock.Setup(m => m.FindByIdAsync("dep-chat-global"))
            .ReturnsAsync(globalDeployment);

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentType.Chat);

        Assert.NotNull(result);
        Assert.Equal("dep-chat-global", result.ItemId);
    }

    [Fact]
    public async Task ResolveUtilityOrDefaultAsync_FallsBackToChat_WhenNoUtilityFound()
    {
        // No utility deployment exists, but a chat deployment does.
        var chatDeployment = CreateDeployment("dep-chat", "gpt-4o", AIDeploymentType.Chat, isDefault: true, connectionName: "conn-1");

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(new[] { chatDeployment });

        _storeMock.Setup(m => m.FindByIdAsync("dep-chat"))
            .ReturnsAsync(chatDeployment);

        var result = await _manager.ResolveUtilityOrDefaultAsync(
            clientName: "openai",
            connectionName: "conn-1");

        Assert.NotNull(result);
        Assert.Equal("dep-chat", result.ItemId);
        Assert.Equal(AIDeploymentType.Chat, result.Type);
    }

    [Fact]
    public async Task ResolveOrDefaultAsync_UtilityWithoutFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentType.Utility);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithNoFallbacks_ReturnsNull()
    {
        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync([]);

        var result = await _manager.ResolveOrDefaultAsync(AIDeploymentType.Chat);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_WithNoFallbacks_ThrowsInvalidOperationException()
    {
        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync([]);

        var manager = (IAIDeploymentManager)_manager;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await manager.ResolveAsync(AIDeploymentType.Chat));
    }

    [Fact]
    public async Task GetAllByTypeAsync_ReturnsOnlyMatchingType()
    {
        var allDeployments = new[]
        {
            CreateDeployment("dep-chat-1", "gpt-4", AIDeploymentType.Chat, clientName: "openai"),
            CreateDeployment("dep-chat-2", "gpt-4o", AIDeploymentType.Chat, clientName: "azure"),
            CreateDeployment("dep-chat-utility", "gpt-4.1-mini", AIDeploymentType.Chat | AIDeploymentType.Utility, clientName: "openai"),
            CreateDeployment("dep-embed-1", "ada-002", AIDeploymentType.Embedding, clientName: "openai"),
            CreateDeployment("dep-img-1", "dall-e-3", AIDeploymentType.Image, clientName: "openai"),
        };

        _storeMock.Setup(m => m.GetAllAsync())
            .ReturnsAsync(allDeployments);

        var result = (await _manager.GetAllByTypeAsync(AIDeploymentType.Chat)).ToList();

        Assert.Equal(3, result.Count);
        Assert.All(result, d => Assert.True(d.SupportsType(AIDeploymentType.Chat)));
        Assert.Contains(result, d => d.ItemId == "dep-chat-1");
        Assert.Contains(result, d => d.ItemId == "dep-chat-2");
        Assert.Contains(result, d => d.ItemId == "dep-chat-utility");
    }

    private static AIDeployment CreateDeployment(
        string itemId,
        string name,
        AIDeploymentType type,
        bool isDefault = false,
        string clientName = "openai",
        string connectionName = "default")
    {
        return new AIDeployment
        {
            ItemId = itemId,
            Name = name,
            Type = type,
            IsDefault = isDefault,
            ClientName = clientName,
            ConnectionName = connectionName,
        };
    }
}
