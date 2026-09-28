using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves a user sees a queue's shared voicemail only when they both hold the shared voicemail permission and are
/// entitled to that queue, so holding the permission never opens another team's messages.
/// </summary>
public sealed class SharedVoicemailAuthorizationServiceTests
{
    [Fact]
    public async Task AUserWithoutThePermission_SeesNoBoxEvenForTheirOwnQueues()
    {
        // Arrange
        var service = CreateService(
            granted: [],
            agent: new AgentProfile { ItemId = "agent-1", UserId = "user-1", AllowedQueueIds = ["queue-main"] });

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(access.CanAccess);
        Assert.False(access.CoversQueue("queue-main"));
    }

    [Fact]
    public async Task AnAgentWithThePermission_SeesOnlyTheQueuesTheyAreEntitledTo()
    {
        // Arrange
        var service = CreateService(
            granted: [ContactCenterPermissions.AccessSharedVoicemail],
            agent: new AgentProfile { ItemId = "agent-1", UserId = "user-1", AllowedQueueIds = ["queue-main"] });

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(access.CanAccess);
        Assert.False(access.AllQueues);
        Assert.False(access.CanManage);
        Assert.Equal("user-1", access.UserId);
        Assert.Equal("agent.one", access.UserName);
        Assert.True(access.CoversQueue("queue-main"));
        Assert.False(access.CoversQueue("queue-billing"));
    }

    [Fact]
    public async Task AQueueTheEntitlementPolicyNoLongerAllows_IsNotSeen_UnlessTheUserOverseesIt()
    {
        // Arrange
        // The Agent Entitlements feature narrowed the agent's queues, but the supervisor authorization still lets the
        // user oversee one of them.
        var policy = new Mock<IAgentEntitlementPolicy>();
        policy.Setup(value => value.AllowsQueue(It.IsAny<AgentProfile>(), It.IsAny<string>())).Returns(false);

        var supervisors = new Mock<ISupervisorQueueAuthorizationService>();
        supervisors
            .Setup(value => value.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), "user-1", "queue-billing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(
            granted: [ContactCenterPermissions.AccessSharedVoicemail],
            agent: new AgentProfile { ItemId = "agent-1", UserId = "user-1", AllowedQueueIds = ["queue-main", "queue-billing"] },
            policy: policy.Object,
            supervisors: supervisors.Object);

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(access.CoversQueue("queue-main"));
        Assert.True(access.CoversQueue("queue-billing"));
    }

    [Fact]
    public async Task AUserWithNoAgentProfile_SeesNoQueue()
    {
        // Arrange
        var service = CreateService(granted: [ContactCenterPermissions.AccessSharedVoicemail], agent: null);

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(access.CanAccess);
        Assert.Empty(access.QueueIds);
        Assert.False(access.CoversQueue("queue-main"));
    }

    [Fact]
    public async Task AContactCenterManager_SeesEveryQueue()
    {
        // Arrange
        var service = CreateService(
            granted: [ContactCenterPermissions.AccessSharedVoicemail, ContactCenterPermissions.ManageSharedVoicemail, ContactCenterPermissions.ManageContactCenter],
            agent: null);

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(access.AllQueues);
        Assert.True(access.CanManage);
        Assert.True(access.CoversQueue("queue-anything"));
    }

    [Fact]
    public async Task ManagingSharedVoicemail_DoesNotWidenTheQueues()
    {
        // Arrange
        var service = CreateService(
            granted: [ContactCenterPermissions.AccessSharedVoicemail, ContactCenterPermissions.ManageSharedVoicemail],
            agent: new AgentProfile { ItemId = "agent-1", UserId = "user-1", AllowedQueueIds = ["queue-main"] });

        // Act
        var access = await service.GetAccessAsync(CreatePrincipal(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(access.CanManage);
        Assert.False(access.AllQueues);
        Assert.False(access.CoversQueue("queue-billing"));
    }

    [Fact]
    public async Task AnAnonymousUser_SeesNothing()
    {
        // Arrange
        var service = CreateService(granted: [ContactCenterPermissions.AccessSharedVoicemail], agent: null);

        // Act
        var access = await service.GetAccessAsync(new ClaimsPrincipal(new ClaimsIdentity()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(SharedVoicemailAccess.None, access);
    }

    internal static Mock<IAuthorizationService> CreateAuthorization(params Permission[] granted)
    {
        var names = granted.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(value => value.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => names.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization;
    }

    internal static ClaimsPrincipal CreatePrincipal(string userId = "user-1", string userName = "agent.one")
        => new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
            ],
            "Test"));

    private static SharedVoicemailAuthorizationService CreateService(
        Permission[] granted,
        AgentProfile agent,
        IAgentEntitlementPolicy policy = null,
        ISupervisorQueueAuthorizationService supervisors = null)
    {
        var agents = new Mock<IAgentProfileManager>();
        agents
            .Setup(value => value.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        return new SharedVoicemailAuthorizationService(
            CreateAuthorization(granted).Object,
            agents.Object,
            policy ?? new EnforcingAgentEntitlementPolicy(),
            supervisors ?? new Mock<ISupervisorQueueAuthorizationService>().Object);
    }
}
