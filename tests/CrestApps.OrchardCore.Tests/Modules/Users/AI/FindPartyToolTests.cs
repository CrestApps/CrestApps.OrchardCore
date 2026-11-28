using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.Users.AI;

/// <summary>
/// <c>findParty</c> searches the party directory for the kinds the caller may see: users with the user picker's
/// permission, customers and staff with their pages' read permissions, and accounts and contacts item by item with
/// <c>ViewContent</c>.
/// </summary>
public sealed class FindPartyToolTests
{
    private readonly Mock<IPartyDirectory> _directory = new();
    private readonly Mock<IContentManager> _contentManager = new();

    public FindPartyToolTests()
    {
        _directory.SetupGet(directory => directory.Kinds).Returns(new[] { PartyKinds.User, PartyKinds.Customer, PartyKinds.Account, "Staff" });
        _directory.Setup(directory => directory.SearchAsync(PartyKinds.User, "north", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PartyOption("u-1", "Nora North (nora@example.com)") });
        _directory.Setup(directory => directory.SearchAsync(PartyKinds.Customer, "north", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PartyOption("c-1", "Northwind Traders") });
        _directory.Setup(directory => directory.SearchAsync(PartyKinds.Account, "north", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new PartyOption("a-1", "Northwind"), new PartyOption("a-2", "Northern Secret") });
        _directory.Setup(directory => directory.SearchAsync("Staff", "north", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PartyOption>());
        _contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => ids.Select(id => new ContentItem { ContentItemId = id, ContentType = "Account" }).ToList().AsEnumerable());
    }

    [Fact]
    public async Task FindParty_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = CreateServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["query"] = "north" });

        // Assert
        Assert.Equal(UsersAIToolBase.NotAllowedMessage, result);
        _directory.Verify(directory => directory.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindParty_RefusesACallerWhoMaySeeNoneOfTheKinds()
    {
        // Arrange
        var services = CreateServices(authorize: (_, _) => false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["query"] = "north", ["kind"] = "Customer" });

        // Assert
        Assert.Equal("Nothing was searched: you are not allowed to see Customer records.", result);
        Assert.Equal("ViewCustomers", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        _directory.Verify(directory => directory.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindParty_SearchesTheKindsTheCallerMaySee_AndChecksEachContentItem()
    {
        // Arrange
        var services = CreateServices(authorize: (permission, resource) => permission.Name switch
        {
            "SearchUsers" => false,
            "ViewContent" => resource is ContentItem item && item.ContentItemId == "a-1",
            _ => true,
        });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["query"] = "north" }));

        // Assert
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(new[] { "Customer:c-1", "Account:a-1" }, items.Select(item => item.GetProperty("party").GetString()));
        Assert.Equal(new[] { "Customer", "Account", "Staff" }, json.GetProperty("searchedKinds").EnumerateArray().Select(kind => kind.GetString()));
        Assert.Equal("User", Assert.Single(json.GetProperty("notAllowedKinds").EnumerateArray()).GetString());
        Assert.DoesNotContain(items, item => item.GetProperty("name").GetString() == "Northern Secret");
    }

    [Fact]
    public async Task FindParty_RefusesAnUnknownKind_AndAKindNoFeatureProvides()
    {
        // Arrange
        var services = CreateServices();

        // Act
        var unknown = await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["query"] = "north", ["kind"] = "Vendor" });
        var missing = await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["query"] = "north", ["kind"] = "Contact" });
        var empty = await AgentToolTestContext.InvokeAsync(new FindPartyTool(), services, new() { ["kind"] = "User" });

        // Assert
        Assert.Equal("Nothing was searched: kind must be one of User, Customer, Account, Contact, Staff, not 'Vendor'.", unknown);
        Assert.Equal("Nothing was searched: no enabled feature provides Contact records on this site.", missing);
        Assert.Equal("Nothing was searched: give the name to look for in query.", empty);
    }

    [Fact]
    public void FindParty_IsRegisteredWithItsTexts_WithoutApproval_ForTheUsersFeature()
    {
        // Arrange
        var services = new ServiceCollection();
        new PartiesAIToolsStartup(new PassThroughStringLocalizer<PartiesAIToolsStartup>()).ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        // Act
        var tools = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
        var feature = typeof(PartiesAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();
        var required = typeof(PartiesAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

        // Assert
        var tool = Assert.Single(tools).Value;

        Assert.Equal(FindPartyTool.TheName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
        Assert.Equal(UsersConstants.Feature.Area, feature.FeatureName);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
    }

    private ServiceProvider CreateServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        services.AddSingleton(_directory.Object);
        services.AddSingleton(_contentManager.Object);

        return services.BuildServiceProvider();
    }
}
