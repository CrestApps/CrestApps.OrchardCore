using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public class SmsConversationAuthorizationServiceTests
{
    private const string UserId = "user-1";
    private const string AgentId = "agent-1";
    private const string OtherAgentId = "agent-2";
    private const string QueueId = "queue-1";

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Claim)]
    [InlineData(ConversationOperation.Close)]
    [InlineData(ConversationOperation.Snooze)]
    [InlineData(ConversationOperation.Transfer)]
    public async Task AuthorizeAsync_WhenPrincipalCanViewAllConversations_AllowsEveryOperation(ConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: OtherAgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: true, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Close)]
    public async Task AuthorizeAsync_WhenPersonalThreadBelongsToAnotherAgent_Denies(ConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: OtherAgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Close)]
    [InlineData(ConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenPersonalThreadIsOwnedByCaller_Allows(ConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Claim)]
    [InlineData(ConversationOperation.Send)]
    public async Task AuthorizeAsync_WhenPersonalThreadIsUnassigned_AllowsViewClaimAndSend(ConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = ConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerHasNoAgentProfile_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = ConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: null);

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Claim)]
    public async Task AuthorizeAsync_WhenQueueMemberAndThreadIsPooled_AllowsViewAndClaim(ConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Pooled);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(ConversationAssignmentStatus.Unassigned)]
    [InlineData(ConversationAssignmentStatus.Pooled)]
    public async Task AuthorizeAsync_WhenQueueMemberAndThreadIsUnclaimed_AllowsSend_BecauseTheReplyClaimsIt(ConversationAssignmentStatus assignmentStatus)
    {
        // Answering an unclaimed thread is taking it: the reply claims it for the sender, so whoever may claim it
        // may also answer it, exactly as on an unowned personal thread.
        var conversation = CreateQueueConversation(assignedAgentId: null, assignmentStatus);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Send, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.Close)]
    [InlineData(ConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenQueueMemberAndThreadIsUnclaimed_StillDeniesClosingIt(ConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Unassigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenCallerIsNotAQueueMember_DeniesView()
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Pooled);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: ["other-queue"], allowedQueueIds: ["other-queue"]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Close)]
    [InlineData(ConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenQueueThreadIsAssignedToAnotherAgent_DeniesWriteOperations(ConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: OtherAgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Close)]
    [InlineData(ConversationOperation.Snooze)]
    public async Task AuthorizeAsync_WhenQueueThreadIsAssignedToCaller_AllowsWriteOperations(ConversationOperation operation)
    {
        var conversation = CreateQueueConversation(assignedAgentId: AgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenEntitlementsAreEnforcedAndQueueIsNotGranted_Denies()
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Pooled);

        // The agent is signed in to the queue but the manager has not granted the queue on the profile.
        var service = CreateService(
            canViewAll: false,
            agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: []),
            entitlementPolicy: new EnforcingAgentEntitlementPolicy());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenTransferRequestedWithoutSupervisorPermission_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenPrincipalIsAnonymous_Denies()
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = ConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    private static MessagingConversation CreatePersonalConversation(string ownerId, string assignedAgentId)
        => new()
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = ownerId,
            AssignedAgentId = assignedAgentId,
            AssignmentStatus = string.IsNullOrEmpty(assignedAgentId)
                ? ConversationAssignmentStatus.Unassigned
                : ConversationAssignmentStatus.Assigned,
        };

    private static MessagingConversation CreateQueueConversation(string assignedAgentId, ConversationAssignmentStatus assignmentStatus)
        => new()
        {
            ItemId = "conv-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Queue,
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

    private static MessagingConversationAuthorizationService CreateService(
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
                    .Any(requirement => requirement.Permission.Name == MessagingPermissions.ViewAllConversations.Name);

                return canViewAll && wantsViewAll
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

        var agentProfileManager = new Mock<IAgentProfileManager>();

        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        return new MessagingConversationAuthorizationService(
            authorizationService.Object,
            agentProfileManager.Object,
            entitlementPolicy ?? new PermissiveAgentEntitlementPolicy());
    }
}
