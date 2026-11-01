using System.Text.Json;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpBrowserToolRegistryProviderTests
{
    private static readonly IReadOnlyList<WebMcpBrowserTool> _tools =
    [
        new WebMcpBrowserTool
        {
            Name = "page_get_info",
            Description = "Describes the page.",
            InputSchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone(),
        },
    ];

    [Fact]
    public async Task GetToolsAsync_AddsThePageToolsOfTheSessionsUser()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: true, registerTools: true);
        var context = CreateContext(new AIChatSession
        {
            SessionId = "session-1",
            ProfileId = "profile-1",
            UserId = "user-1",
        });

        // Act
        var entries = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(entries);
        Assert.Equal("webmcp:page_get_info", entry.Id);
        Assert.Equal("page_get_info", entry.Name);
        Assert.Equal("Describes the page.", entry.Description);

        var tool = await entry.CreateAsync(null);

        var function = Assert.IsType<WebMcpBrowserToolFunction>(tool);
        Assert.Equal("page_get_info", function.Name);
    }

    [Fact]
    public async Task GetToolsAsync_AddsNothingWhenTheProfileDoesNotAllowPageTools()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: false, registerTools: true);
        var context = CreateContext(new AIChatSession
        {
            ProfileId = "profile-1",
            UserId = "user-1",
        });

        // Act
        var entries = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetToolsAsync_AddsNothingWhenTheUserHasNoPageTools()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: true, registerTools: false);
        var context = CreateContext(new AIChatSession
        {
            ProfileId = "profile-1",
            UserId = "user-1",
        });

        // Act
        var entries = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetToolsAsync_AddsNothingForAnAnonymousSession()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: true, registerTools: true);
        var context = CreateContext(new AIChatSession
        {
            ProfileId = "profile-1",
            ClientId = "visitor-1",
        });

        // Act
        var entries = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetToolsAsync_AddsNothingToAnotherUsersSession()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: true, registerTools: true);
        var context = CreateContext(new AIChatSession
        {
            ProfileId = "profile-1",
            UserId = "user-2",
        });

        // Act
        var entries = await provider.GetToolsAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetToolsAsync_AddsNothingWithoutASessionOrWhenToolsAreOff()
    {
        // Arrange
        var provider = CreateProvider(allowPageTools: true, registerTools: true);
        var disabled = CreateContext(new AIChatSession
        {
            ProfileId = "profile-1",
            UserId = "user-1",
        });

        disabled.DisableTools = true;

        // Act & Assert
        Assert.Empty(await provider.GetToolsAsync(new AICompletionContext(), TestContext.Current.CancellationToken));
        Assert.Empty(await provider.GetToolsAsync(null, TestContext.Current.CancellationToken));
        Assert.Empty(await provider.GetToolsAsync(disabled, TestContext.Current.CancellationToken));
    }

    private static AICompletionContext CreateContext(AIChatSession session)
    {
        var context = new AICompletionContext();

        context.AdditionalProperties[AICompletionContextKeys.Session] = session;

        return context;
    }

    private static WebMcpBrowserToolRegistryProvider CreateProvider(bool allowPageTools, bool registerTools)
    {
        var registry = new DefaultWebMcpBrowserToolRegistry();

        if (registerTools)
        {
            registry.Register("user-1", "connection-1", _tools);
        }

        var profile = new AIProfile
        {
            ItemId = "profile-1",
        };

        profile.AlterSettings<WebMcpProfileSettings>(settings => settings.AllowPageTools = allowPageTools);

        var profileManager = new Mock<IAIProfileManager>();

        profileManager
            .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        return new WebMcpBrowserToolRegistryProvider(registry, Mock.Of<IWebMcpBrowserToolInvoker>(), profileManager.Object);
    }
}
