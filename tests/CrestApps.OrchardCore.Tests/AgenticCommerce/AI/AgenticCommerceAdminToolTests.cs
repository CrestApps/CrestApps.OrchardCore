using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Environment.Shell;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce.AI;

/// <summary>
/// The agentic commerce admin tools need the permission of the page they mirror, list agent checkouts without the
/// buyer, the addresses or the platform's protocol data, report the agent feed without its credentials, and push it only
/// when a push is set up. The AI agent platforms are the <c>agentPlatform</c> record kind, whose projection never returns
/// the API key hash or the webhook secret.
/// </summary>
public sealed class AgenticCommerceAdminToolTests
{
    private const string Canary = "CANARY-SECRET";

    private readonly AgentCheckout _checkout = new()
    {
        ItemId = "checkout-1",
        PlatformId = "platform-1",
        Protocol = "acp",
        ProtocolVersion = "2026-01-30",
        Status = AgentCheckoutStatus.Canceled,
        Currency = "USD",
        OrderId = "order-1",
        Lines = [new AgentCheckoutLineInput { ItemId = "mug", Quantity = 2 }],
        Buyer = new AgentBuyer { FirstName = Canary, Email = Canary + "@example.com" },
        EscalationUrl = "https://store.example.com/" + Canary,
        ProtocolData = new Dictionary<string, string> { ["token"] = Canary },
        PaymentMessage = AgentCheckoutMessage.Error("payment_declined", "payment", "The card was declined."),
        CreatedUtc = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
        ExpiresUtc = new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc),
    };

    private readonly AgentPlatform _platform = new()
    {
        ItemId = "platform-1",
        Name = "ChatGPT",
        Protocols = ["acp"],
        ApiKeyHash = Canary,
        ApiKeyHint = "…abcd",
        ProtectedWebhookSecret = Canary,
    };

    private readonly Mock<IAgentCheckoutStore> _checkouts = new();
    private readonly Mock<IAgentPlatformManager> _platforms = new();
    private readonly Mock<IOrderStore> _orders = new();
    private readonly AgentFeedSettings _feedSettings = new();
    private AgentCheckoutQuery _query;

    public AgenticCommerceAdminToolTests()
    {
        _checkouts.Setup(store => store.PageAsync(It.IsAny<AgentCheckoutQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((AgentCheckoutQuery query, int _, int _, CancellationToken _) => _query = query)
            .ReturnsAsync((1, (IReadOnlyList<AgentCheckout>)[_checkout]));
        _checkouts.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentCheckout)null);
        _platforms.Setup(manager => manager.FindByIdAsync("platform-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_platform);
        _orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Order { ItemId = "order-1", OrderNumber = "1042" });
    }

    public static TheoryData<string, string> Tools => new()
    {
        { ListAgentCheckoutsTool.TheName, AgenticCommercePermissions.ManageAgentPlatforms.Name },
        { GetAgentFeedStatusTool.TheName, AgentFeedPermissions.ManageAgentFeeds.Name },
        { PushAgentFeedsTool.TheName, AgentFeedPermissions.ManageAgentFeeds.Name },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), []);

        // Assert
        Assert.Equal(AgenticCommerceAdminToolBase.NotAllowedMessage, result);
        Assert.NotNull(permission);
        Assert.Empty(_checkouts.Invocations);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutThePagesPermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, []);

        // Assert
        Assert.Equal(AgenticCommerceAdminToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_checkouts.Invocations);
    }

    [Fact]
    public async Task ListAgentCheckouts_FiltersAndReturnsTheListedFieldsOnly()
    {
        // Act
        var result = await InvokeAsync(ListAgentCheckoutsTool.TheName, Build(), new()
        {
            ["platformId"] = "platform-1",
            ["state"] = "ended",
            ["hasOrder"] = false,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("platform-1", _query.PlatformId);
        Assert.True(_query.Ended);
        Assert.False(_query.HasOrder);
        Assert.Equal("ChatGPT", item.GetProperty("platform").GetString());
        Assert.Equal("canceled", item.GetProperty("status").GetString());
        Assert.Equal("1042", item.GetProperty("orderNumber").GetString());
        Assert.Equal(2, item.GetProperty("itemCount").GetInt32());
        Assert.Equal("payment_declined", item.GetProperty("failure").GetProperty("code").GetString());
        Assert.True(item.GetProperty("hasEscalationLink").GetBoolean());
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAgentCheckouts_SaysWhenACheckoutIsNotFound()
    {
        // Act
        var result = await InvokeAsync(ListAgentCheckoutsTool.TheName, Build(), new() { ["checkoutId"] = "missing" });

        // Assert
        Assert.StartsWith("No agent checkout has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAgentCheckouts_RefusesAnUnknownState()
    {
        // Act
        var result = await InvokeAsync(ListAgentCheckoutsTool.TheName, Build(), new() { ["state"] = "failed" });

        // Assert
        Assert.Equal("Nothing was listed: state must be open or ended.", result);
        _checkouts.Verify(store => store.PageAsync(It.IsAny<AgentCheckoutQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAgentFeedStatus_ReportsTheFeedAndThePushes_WithoutCredentials()
    {
        // Arrange
        _feedSettings.Sftp = new AgentFeedSftpSettings
        {
            Enabled = true,
            Host = "sftp.example.com",
            Username = "store",
            Password = Canary,
            PrivateKey = Canary,
        };
        _feedSettings.FeedApiTargets = [new AcpFeedTarget { Id = "target-1", Name = "OpenAI", BaseUrl = "https://api.example.com/feeds", BearerToken = Canary }];

        var statuses = new InMemoryDocumentManager<AgentFeedStatusDocument>();

        await statuses.UpdateAsync(new AgentFeedStatusDocument
        {
            LastSftpPush = new AgentFeedPushResult { Succeeded = false, Message = "Connection refused." },
        });

        var services = Build(configure: collection => collection.AddSingleton<IDocumentManager<AgentFeedStatusDocument>>(statuses));

        // Act
        var result = await InvokeAsync(GetAgentFeedStatusTool.TheName, services, []);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(0, json.GetProperty("feed").GetProperty("products").GetInt32());
        Assert.Equal(1, json.GetProperty("feed").GetProperty("leftOut").GetInt32());
        Assert.Equal("No price.", Assert.Single(json.GetProperty("feed").GetProperty("problems").EnumerateArray()).GetProperty("problem").GetString());
        Assert.True(json.GetProperty("sftp").GetProperty("hasPassword").GetBoolean());
        Assert.False(json.GetProperty("sftp").GetProperty("lastPush").GetProperty("succeeded").GetBoolean());
        Assert.Equal("api.example.com", Assert.Single(json.GetProperty("feedApiTargets").EnumerateArray()).GetProperty("host").GetString());
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushAgentFeeds_SaysNothingWasPushed_WhenNoPushIsSetUp()
    {
        // Act
        var result = await InvokeAsync(PushAgentFeedsTool.TheName, Build(), []);

        // Assert
        Assert.StartsWith("Nothing was pushed", result, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdminStartup_RegistersTheAgentPlatformKind_WithTheAgentPlatformsPermission()
    {
        // Arrange
        var services = new ServiceCollection();

        new AdminAIToolsStartup(new RecordTestLocalizer<AdminAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var kind = Assert.IsType<AIRecordKind<AgentPlatform>>(provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds["agentPlatform"]);
        var projected = System.Text.Json.JsonSerializer.Serialize(kind.Projection(_platform));

        // Assert
        Assert.Equal(AgenticCommercePermissions.ManageAgentPlatforms.Name, kind.Permission.Name);
        Assert.Equal(AgenticCommercePermissions.ManageAgentPlatforms.Name, kind.EffectiveReadPermission.Name);
        Assert.True(kind.CanWrite);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
        Assert.Contains("\"hasApiKey\":true", projected, StringComparison.Ordinal);
        Assert.Contains("\"hasWebhookSecret\":true", projected, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, projected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRecord_ReturnsAnAgentPlatform_WithoutItsSecrets()
    {
        // Arrange
        var services = Build(configure: collection =>
        {
            collection.AddScoped<AIRecordScopeChanges>();
            new AdminAIToolsStartup(new RecordTestLocalizer<AdminAIToolsStartup>()).ConfigureServices(collection);
        });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = "agentPlatform",
            ["id"] = "platform-1",
        });

        // Assert
        Assert.Contains("ChatGPT", result, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, result, StringComparison.Ordinal);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null, Action<IServiceCollection> configure = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<AgentFeedSettings>()).Returns(() => _feedSettings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var productFeeds = new Mock<IProductFeedService>();
        productFeeds.Setup(feeds => feeds.GetAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductFeed([], [new ProductFeedProblem("mug", "Classic mug", "No price.", LeftOut: true)], new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc)));

        services.AddSingleton(_checkouts.Object);
        services.AddSingleton(_platforms.Object);
        services.AddSingleton(_orders.Object);
        services.AddSingleton(siteService.Object);
        services.AddSingleton(productFeeds.Object);
        services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        services.AddSingleton(new ShellSettings { Name = "Default" });
        services.AddSingleton<IDocumentManager<AgentFeedStatusDocument>>(new InMemoryDocumentManager<AgentFeedStatusDocument>());
        services.AddSingleton<OpenAIProductFeedService>();

        // The SFTP and feed API pushers are only asked whether a push is set up; with none, they never connect.
        services.AddSingleton(provider => new AgentFeedPushService(
            provider.GetRequiredService<ISiteService>(),
            new AgentFeedSftpPusher(provider.GetRequiredService<ISiteService>(), null, null, null, null, null),
            new AcpFeedPusher(provider.GetRequiredService<ISiteService>(), null, null, null, null, null, null)));

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListAgentCheckoutsTool.TheName => new ListAgentCheckoutsTool(),
            GetAgentFeedStatusTool.TheName => new GetAgentFeedStatusTool(),
            _ => new PushAgentFeedsTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
