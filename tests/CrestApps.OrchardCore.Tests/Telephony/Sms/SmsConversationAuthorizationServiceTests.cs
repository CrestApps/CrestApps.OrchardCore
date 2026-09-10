using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsConversationAuthorizationServiceTests
{
    private const string UserId = "user-1";
    private const string AgentId = "agent-1";
    private const string OtherAgentId = "agent-2";
    private const string QueueId = "queue-1";

    [Theory]
    [InlineData(SmsConversationOperation.View)]
    [InlineData(SmsConversationOperation.Send)]
    [InlineData(SmsConversationOperation.Claim)]
    [InlineData(SmsConversationOperation.Close)]
    [InlineData(SmsConversationOperation.Snooze)]
    [InlineData(SmsConversationOperation.Transfer)]
    public async Task AuthorizeAsync_WhenPrincipalCanViewAllConversations_AllowsEveryOperation(SmsConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: OtherAgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: true, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.View)]
    [InlineData(SmsConversationOperation.Send)]
    [InlineData(SmsConversationOperation.Close)]
    public async Task AuthorizeAsync_WhenPersonalThreadBelongsToAnotherAgent_Denies(SmsConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: OtherAgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.View)]
    [InlineData(SmsConversationOperation.Send)]
    [InlineData(SmsConversationOperation.Close)]
    [InlineData(SmsConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenPersonalThreadIsOwnedByCaller_Allows(SmsConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.View)]
    [InlineData(SmsConversationOperation.Claim)]
    [InlineData(SmsConversationOperation.Send)]
    public async Task AuthorizeAsync_WhenPersonalThreadIsUnassigned_AllowsViewClaimAndSend(SmsConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerHasNoAgentProfile_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: null);

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, SmsConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.View)]
    [InlineData(SmsConversationOperation.Claim)]
    public async Task AuthorizeAsync_WhenQueueMemberAndThreadIsPooled_AllowsViewAndClaim(SmsConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, SmsConversationAssignmentStatus.Pooled);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerIsNotAQueueMember_DeniesView()
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, SmsConversationAssignmentStatus.Pooled);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: ["other-queue"], allowedQueueIds: ["other-queue"]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, SmsConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.Send)]
    [InlineData(SmsConversationOperation.Close)]
    [InlineData(SmsConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenQueueThreadIsAssignedToAnotherAgent_DeniesWriteOperations(SmsConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: OtherAgentId, SmsConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(SmsConversationOperation.Send)]
    [InlineData(SmsConversationOperation.Close)]
    [InlineData(SmsConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenQueueThreadIsAssignedToCaller_AllowsWriteOperations(SmsConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: AgentId, SmsConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenEntitlementsAreEnforcedAndQueueIsNotGranted_Denies()
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, SmsConversationAssignmentStatus.Pooled);

        // The agent is signed in to the queue but the manager has not granted the queue on the profile.
        var service = CreateService(
            canViewAll: false,
            agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: []),
            entitlementPolicy: new EnforcingAgentEntitlementPolicy());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, SmsConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenTransferRequestedWithoutSupervisorPermission_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, SmsConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPrincipalIsAnonymous_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = SmsConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), conversation, SmsConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    private static SmsConversation CreatePersonalConversation(string ownerId, string assignedAgentId)
        => new()
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            OwnerId = ownerId,
            AssignedAgentId = assignedAgentId,
            AssignmentStatus = string.IsNullOrEmpty(assignedAgentId)
                ? SmsConversationAssignmentStatus.Unassigned
                : SmsConversationAssignmentStatus.Assigned,
        };

    private static SmsConversation CreateQueueConversation(string assignedAgentId, SmsConversationAssignmentStatus assignmentStatus)
        => new()
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Queue,
            OwnerId = QueueId,
            AssignedAgentId = assignedAgentId,
            AssignmentStatus = assignmentStatus,
        };

    private static AgentProfile CreateAgent(IList<string> queueIds = null, IList<string> allowedQueueIds = null)
        => new()
        {
            ItemId = AgentId,
            UserId = UserId,
            QueueIds = queueIds ?? [],
            AllowedQueueIds = allowedQueueIds ?? [],
        };

    private static ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"));

    private static SmsConversationAuthorizationService CreateService(
        bool canViewAll,
        AgentProfile agent,
        IAgentEntitlementPolicy entitlementPolicy = null)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
            {
                var wantsViewAll = requirements
                    .OfType<PermissionRequirement>()
                    .Any(requirement => requirement.Permission.Name == SmsPortalPermissions.ViewAllConversations.Name);

                return canViewAll && wantsViewAll
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

        var agentProfileManager = new Mock<IAgentProfileManager>();

        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        return new SmsConversationAuthorizationService(
            authorizationService.Object,
            agentProfileManager.Object,
            entitlementPolicy ?? new PermissiveAgentEntitlementPolicy());
    }
}
