using CrestApps.OrchardCore.Memberships;
using CrestApps.OrchardCore.Memberships.AI;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Services;
using MembershipService = CrestApps.OrchardCore.Memberships.Services.MembershipService;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Memberships.AI;

/// <summary>
/// The membership tools need Manage memberships, list through the memberships page's query, and end only an active
/// membership, through the membership service, which takes the member out of its roles.
/// </summary>
public sealed class MembershipsAIToolTests
{
    private readonly Membership _active = new()
    {
        ItemId = "membership-1",
        UserId = "member-1",
        UserName = "sam",
        Title = "Gold membership",
        ContentItemId = "gold",
        RoleNames = ["Gold"],
        Status = MembershipStatus.Active,
        StartsUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndsUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private readonly Membership _expired = new()
    {
        ItemId = "membership-2",
        UserId = "member-2",
        UserName = "kim",
        Title = "Gold membership",
        Status = MembershipStatus.Expired,
    };

    private readonly Mock<IMembershipStore> _store = new();
    private readonly Mock<IRoleGrantService> _roleGrants = new();
    private MembershipStatus? _listedStatus;

    public MembershipsAIToolTests()
    {
        _store.Setup(store => store.FindByIdAsync("membership-1", It.IsAny<CancellationToken>())).ReturnsAsync(_active);
        _store.Setup(store => store.FindByIdAsync("membership-2", It.IsAny<CancellationToken>())).ReturnsAsync(_expired);
        _store.Setup(store => store.PageAsync(It.IsAny<string>(), It.IsAny<MembershipStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((string _, MembershipStatus? status, int _, int _, CancellationToken _) => _listedStatus = status)
            .ReturnsAsync((1, (IReadOnlyList<Membership>)[_active]));
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListMembershipsTool.TheName,
        EndMembershipTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(MembershipsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_store.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageMemberships(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != MembershipsPermissions.ManageMemberships.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(MembershipsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(MembershipsPermissions.ManageMemberships.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_store.Invocations);
    }

    [Fact]
    public async Task ListMemberships_FiltersByStatus_AndReturnsTheListedFields()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListMembershipsTool.TheName, Build(), new() { ["status"] = "endedEarly" }));
        var wrong = await InvokeAsync(ListMembershipsTool.TheName, Build(), new() { ["status"] = "lapsed" });

        // Assert
        Assert.Equal(MembershipStatus.Revoked, _listedStatus);
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("sam", item.GetProperty("member").GetString());
        Assert.Equal("active", item.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
        Assert.StartsWith("Nothing was listed", wrong, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndMembership_EndsAnActiveMembership_AndTakesTheMemberOutOfItsRoles()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(EndMembershipTool.TheName, Build(), new() { ["membershipId"] = "membership-1", ["reason"] = "Asked by phone" }));

        // Assert
        Assert.Equal(MembershipStatus.Revoked, _active.Status);
        Assert.Equal($"Ended by {AgentToolTestContext.UserName}: Asked by phone", _active.EndedReason);
        Assert.False(json.GetProperty("refunded").GetBoolean());
        _roleGrants.Verify(grants => grants.RevokeAsync("member-1", "Gold", MembershipRoleGrantSource.SourceName, "membership-1"), Times.Once);
        _store.Verify(store => store.UpdateAsync(_active, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndMembership_RefusesAMembershipThatIsNotActive()
    {
        // Act
        var result = await InvokeAsync(EndMembershipTool.TheName, Build(), new() { ["membershipId"] = "membership-2" });
        var missing = await InvokeAsync(EndMembershipTool.TheName, Build(), new() { ["membershipId"] = "nope" });

        // Assert
        Assert.Contains("only an active membership can be ended", result, StringComparison.Ordinal);
        Assert.Contains("no membership has the id 'nope'", missing, StringComparison.Ordinal);
        Assert.Equal(MembershipStatus.Expired, _expired.Status);
        _store.Verify(store => store.UpdateAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            EndMembershipTool.TheName => new() { ["membershipId"] = "membership-1" },
            _ => [],
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        services.AddSingleton(_store.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton(new MembershipService(_store.Object, Mock.Of<IUserService>(), _roleGrants.Object, clock.Object));

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListMembershipsTool.TheName => new ListMembershipsTool(),
            _ => new EndMembershipTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
