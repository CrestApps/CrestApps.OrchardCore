using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

// A conversation is authorized against ViewAllMessagingConversations, the way Orchard Core authorizes a content item
// against ViewContent: a supervisor holds it, and the handler grants anybody else the conversations that are theirs.
public class MessagingConversationAuthorizationHandlerTests
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
    public async Task AuthorizeAsync_WhenPersonalThreadHasNoOwner_DeniesAnAgent_BecauseItWaitsForASupervisorToTriage(ConversationOperation operation)
    {
        // A message no route gave to an agent or a queue is nobody's: only a supervisor who sees every conversation
        // may pick it up, not whichever agent opens it first.
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
        conversation.AssignmentStatus = ConversationAssignmentStatus.Unassigned;

        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Claim)]
    [InlineData(ConversationOperation.Send)]
    public async Task AuthorizeAsync_WhenPersonalThreadSentToTheCallerIsUnclaimed_AllowsViewClaimAndSend(ConversationOperation operation)
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: null);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Claim)]
    [InlineData(ConversationOperation.Transfer)]
    public async Task AuthorizeAsync_WhenAColleagueClaimedAThreadSentToTheCaller_Denies(ConversationOperation operation)
    {
        // The endpoint is the caller's, but a colleague holds the conversation now: it is theirs, not the caller's.
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
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

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Claim)]
    [InlineData(ConversationOperation.Send)]
    public async Task AuthorizeAsync_WhenQueueMemberLacksTheQueuePermission_DeniesTheUnclaimedThread(ConversationOperation operation)
    {
        // Using the workspace grants an agent their own conversations only; their queues' shared inbox is a permission
        // of its own.
        var conversation = CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Pooled);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]), canViewQueue: false);

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationOperation.View)]
    [InlineData(ConversationOperation.Send)]
    [InlineData(ConversationOperation.Close)]
    public async Task AuthorizeAsync_WhenTheCallerHoldsAQueueThread_AllowsIt_WithTheOwnPermission_EvenOutsideTheQueue(ConversationOperation operation)
    {
        // What an agent holds is their own: handed it from a queue they never served, or after leaving the queue, they
        // can still finish it.
        var conversation = CreateQueueConversation(assignedAgentId: AgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(), canViewQueue: false);

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, operation, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WithOnlyTheOwnPermission_AllowsTheCallersThread_ButNotTheQueuePool()
    {
        var agent = CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]);
        var service = CreateService(canViewAll: false, agent, canViewQueue: false);

        var own = await service.AuthorizeAsync(CreatePrincipal(), CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId), ConversationOperation.Send, TestContext.Current.CancellationToken);
        var pooled = await service.AuthorizeAsync(CreatePrincipal(), CreateQueueConversation(assignedAgentId: null, ConversationAssignmentStatus.Pooled), ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.True(own);
        Assert.False(pooled);
    }

    [Fact]
    public async Task AuthorizeAsync_WithoutAnyConversationPermission_DeniesEvenTheCallersOwnThread()
    {
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent(), canViewQueue: false, canViewOwn: false);

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenQueueThreadIsClaimedByAnotherMember_DeniesView()
    {
        var conversation = CreateQueueConversation(assignedAgentId: OtherAgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.View, TestContext.Current.CancellationToken);

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
    public async Task AuthorizeAsync_WhenTheCallerHoldsTheirOwnPersonalThread_AllowsTransfer()
    {
        // Whoever is working a conversation can hand it on without asking a supervisor to do it for them.
        var conversation = CreatePersonalConversation(ownerId: AgentId, assignedAgentId: AgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenTheCallerHoldsAQueueThread_AllowsTransfer()
    {
        var conversation = CreateQueueConversation(assignedAgentId: AgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.True(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenTheThreadBelongsToAnotherAgent_DeniesTransfer()
    {
        var conversation = CreatePersonalConversation(ownerId: OtherAgentId, assignedAgentId: OtherAgentId);
        var service = CreateService(canViewAll: false, agent: CreateAgent());

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAQueueThreadIsHeldByAnotherMember_DeniesTransfer()
    {
        var conversation = CreateQueueConversation(assignedAgentId: OtherAgentId, ConversationAssignmentStatus.Assigned);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(ConversationAssignmentStatus.Unassigned)]
    [InlineData(ConversationAssignmentStatus.Pooled)]
    public async Task AuthorizeAsync_WhenAQueueThreadIsUnclaimed_DeniesTransferToAMember_WhoMustClaimItFirst(ConversationAssignmentStatus assignmentStatus)
    {
        var conversation = CreateQueueConversation(assignedAgentId: null, assignmentStatus);
        var service = CreateService(canViewAll: false, agent: CreateAgent(queueIds: [QueueId], allowedQueueIds: [QueueId]));

        var allowed = await service.AuthorizeAsync(CreatePrincipal(), conversation, ConversationOperation.Transfer, TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAPersonalThreadIsUnowned_DeniesTransfer_UntilSomebodyClaimsIt()
    {
        var conversation = CreatePersonalConversation(ownerId: null, assignedAgentId: null);
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

    private static ConversationAuthorizer CreateService(
        bool canViewAll,
        AgentProfile agent,
        IAgentEntitlementPolicy entitlementPolicy = null,
        bool canViewQueue = true,
        bool canViewOwn = true)
    {
        var granted = new List<Permission>();

        if (canViewAll)
        {
            granted.Add(MessagingPermissions.ViewAllConversations);
        }

        if (canViewQueue)
        {
            granted.Add(MessagingPermissions.ViewQueueConversations);
        }

        if (canViewOwn)
        {
            granted.Add(MessagingPermissions.ViewOwnConversations);
        }

        return new ConversationAuthorizer(MessagingTestAuthorization.Create(agent, entitlementPolicy, [.. granted]));
    }

    private sealed class ConversationAuthorizer(IAuthorizationService authorizationService)
    {
        public Task<bool> AuthorizeAsync(ClaimsPrincipal user, MessagingConversation conversation, ConversationOperation operation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return authorizationService.AuthorizeConversationAsync(user, conversation, operation);
        }
    }
}
