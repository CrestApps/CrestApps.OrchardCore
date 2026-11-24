using CrestApps.OrchardCore.Classifieds;
using CrestApps.OrchardCore.Classifieds.AI;
using CrestApps.OrchardCore.Classifieds.Models;
using CrestApps.OrchardCore.Classifieds.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users.Services;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Classifieds.AI;

/// <summary>
/// The listing tools need Moderate posted listings; moderating approves through the listing service, which publishes the
/// listing and starts its term, and refuses what is not a posted listing or not a known action. Listing the listings
/// runs a YesSql index query and is covered by the refusal tests and the argument check only.
/// </summary>
public sealed class ClassifiedsAIToolTests
{
    private readonly ContentItem _listing;
    private readonly Mock<IContentManager> _contentManager = new();

    public ClassifiedsAIToolTests()
    {
        _listing = new ContentItem
        {
            ContentItemId = "listing-1",
            ContentType = "Listing",
            DisplayText = "Mountain bike, barely used",
        };
        _listing.Weld(new ClassifiedPart { Status = ClassifiedStatus.Pending, Place = "Denver" });

        _contentManager.Setup(manager => manager.GetAsync("listing-1", It.IsAny<VersionOptions>())).ReturnsAsync(_listing);
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListClassifiedsTool.TheName,
        ModerateClassifiedTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(ClassifiedsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_contentManager.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutModerateClassifieds(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != ClassifiedsPermissions.ModerateClassifieds.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(ClassifiedsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(ClassifiedsPermissions.ModerateClassifieds.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_contentManager.Invocations);
    }

    [Fact]
    public async Task ListClassifieds_RefusesAnUnknownStatus()
    {
        // Act
        var result = await InvokeAsync(ListClassifiedsTool.TheName, Build(), new() { ["status"] = "spam" });

        // Assert
        Assert.StartsWith("Nothing was listed: status must be", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModerateClassified_Approves_PublishesTheListing_AndStartsItsTerm()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ModerateClassifiedTool.TheName, Build(), new() { ["listingId"] = "listing-1", ["action"] = "approve" }));

        // Assert
        var part = _listing.GetOrCreate<ClassifiedPart>();
        Assert.Equal(ClassifiedStatus.Approved, part.Status);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), part.ExpiresUtc);
        Assert.Equal("pending", json.GetProperty("previousStatus").GetString());
        Assert.Equal("approved", json.GetProperty("listing").GetProperty("status").GetString());
        _contentManager.Verify(manager => manager.PublishAsync(_listing), Times.Once);
    }

    [Theory]
    [InlineData("listing-1", "delete", null, "action must be approve or reject")]
    [InlineData("listing-1", "approve", "Looks fine", "a reason is only sent when a listing is rejected")]
    [InlineData("missing", "approve", null, "no posted listing has the id 'missing'")]
    public async Task ModerateClassified_RefusesWhatItCannotDo(string listingId, string action, string reason, string expected)
    {
        // Arrange
        var arguments = new Dictionary<string, object> { ["listingId"] = listingId, ["action"] = action };

        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        // Act
        var result = await InvokeAsync(ModerateClassifiedTool.TheName, Build(), arguments);

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Equal(ClassifiedStatus.Pending, _listing.GetOrCreate<ClassifiedPart>().Status);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            ModerateClassifiedTool.TheName => new() { ["listingId"] = "listing-1", ["action"] = "reject" },
            _ => [],
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        services.AddSingleton(_contentManager.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton(new ClassifiedService(
            _contentManager.Object,
            new Mock<IContentDefinitionManager>().Object,
            Mock.Of<YesSqlSession>(),
            Mock.Of<ICommerceEmailSender>(),
            clock.Object,
            new PassThroughStringLocalizer<ClassifiedService>(),
            Mock.Of<IUserService>(),
            Mock.Of<ISiteService>()));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListClassifiedsTool.TheName => new ListClassifiedsTool(),
            _ => new ModerateClassifiedTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
