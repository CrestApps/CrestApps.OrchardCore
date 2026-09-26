using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class MessagingConversationTransferServiceTests
{
    private const string ConversationId = "conv-1";
    private const string SenderId = "agent-a";
    private const string RecipientId = "agent-b";
    private const string QueueId = "queue-1";
    private const string OtherQueueId = "queue-2";

    private static readonly DateTime _now = new(2026, 9, 26, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TransferAsync_ToAnotherAgent_ReassignsThePersonalConversationToThem()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        var result = await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(RecipientId, conversation.AssignedAgentId);
        Assert.Equal(RecipientId, conversation.OwnerId);
        Assert.Equal(ConversationOwnerType.Personal, conversation.OwnerType);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
        Assert.Equal(ConversationId, conversation.ItemId);
        context.Store.Verify(store => store.UpdateAsync(conversation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ToAnotherAgent_LeavesTheConversationUnreadForTheRecipient()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        conversation.IsRead = true;
        conversation.AssignedUtc = _now.AddMinutes(-5);
        conversation.ReassignmentAttempts = 2;

        var context = new TestContextBuilder(conversation);

        await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        // A person chose the recipient, so the routed pickup clock does not bounce it on to somebody else.
        Assert.False(conversation.IsRead);
        Assert.Null(conversation.AssignedUtc);
        Assert.Equal(0, conversation.ReassignmentAttempts);
    }

    [Fact]
    public async Task TransferAsync_ToAMemberOfTheOwningQueue_KeepsTheQueueAsTheOwner()
    {
        var conversation = CreateQueueConversation(assignedAgentId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddAgent(RecipientId, "Bea Recipient", queueIds: [QueueId]);

        var result = await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ConversationOwnerType.Queue, conversation.OwnerType);
        Assert.Equal(QueueId, conversation.OwnerId);
        Assert.Equal(RecipientId, conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, conversation.AssignmentStatus);
    }

    [Fact]
    public async Task TransferAsync_ToSomeoneOutsideTheOwningQueue_MakesItTheirPersonalConversation()
    {
        // A queue conversation assigned to someone who does not serve the queue could never be opened by them, so
        // the conversation follows the person rather than staying with a team they cannot see.
        var conversation = CreateQueueConversation(assignedAgentId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddAgent(RecipientId, "Bea Recipient", queueIds: [OtherQueueId]);

        var result = await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ConversationOwnerType.Personal, conversation.OwnerType);
        Assert.Equal(RecipientId, conversation.OwnerId);
        Assert.Equal(RecipientId, conversation.AssignedAgentId);
    }

    [Fact]
    public async Task TransferAsync_ToAQueue_PutsTheConversationBackInThatTeamsSharedPool()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddQueue(QueueId, "Billing");

        var result = await context.Service.TransferAsync(ToQueue(QueueId), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ConversationOwnerType.Queue, conversation.OwnerType);
        Assert.Equal(QueueId, conversation.OwnerId);
        Assert.Null(conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Pooled, conversation.AssignmentStatus);
    }

    [Fact]
    public async Task TransferAsync_ToADisabledQueue_IsRefused()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddQueue(QueueId, "Billing", enabled: false);

        var result = await context.Service.TransferAsync(ToQueue(QueueId), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(SenderId, conversation.AssignedAgentId);
        context.Store.Verify(store => store.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ToAQueue_WhenQueuesAreNotAvailable_IsRefused()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation, withQueues: false);

        var result = await context.Service.TransferAsync(ToQueue(QueueId), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(SenderId, conversation.AssignedAgentId);
    }

    [Fact]
    public async Task TransferAsync_ToTheAgentWhoAlreadyHoldsIt_IsRefused()
    {
        var conversation = CreatePersonalConversation(holderId: RecipientId);
        var context = new TestContextBuilder(conversation);

        var result = await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(conversation.History);
        context.Notifier.Verify(notifier => notifier.ConversationAssignedAsync(It.IsAny<MessagingAssignmentNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransferAsync_ToAnUnknownAgent_IsRefused()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        var result = await context.Service.TransferAsync(ToAgent("agent-nobody"), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(SenderId, conversation.AssignedAgentId);
    }

    [Fact]
    public async Task TransferAsync_WithoutATarget_IsRefused()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        var result = await context.Service.TransferAsync(ToAgent(null), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task TransferAsync_WhenThePrincipalMayNotTransferIt_IsRefusedAndNothingChanges()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation, allowTransfer: false);

        var request = ToAgent(RecipientId);
        request.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-x")], "Test"));

        var result = await context.Service.TransferAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(SenderId, conversation.AssignedAgentId);
        context.Store.Verify(store => store.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
        context.Authorization.Verify(
            service => service.AuthorizeAsync(request.Principal, conversation, ConversationOperation.Transfer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransferAsync_WhenTheConversationDoesNotExist_IsRefused()
    {
        var context = new TestContextBuilder(conversation: null);

        var result = await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task TransferAsync_RecordsWhoMovedItFromWhomToWhom_WithTheNote()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        var request = ToAgent(RecipientId);
        request.Note = "  They are asking about the March invoice.  ";

        var result = await context.Service.TransferAsync(request, TestContext.Current.CancellationToken);

        var entry = Assert.Single(conversation.History);
        Assert.Same(entry, result.Event);
        Assert.Equal(MessagingConversationEventKind.Transferred, entry.Kind);
        Assert.Equal(_now, entry.OccurredUtc);
        Assert.Equal(SenderId, entry.ActorAgentId);
        Assert.Equal("Ann Sender", entry.ActorName);
        Assert.Equal(SenderId, entry.FromAgentId);
        Assert.Equal("Ann Sender", entry.FromName);
        Assert.Equal(RecipientId, entry.ToAgentId);
        Assert.Null(entry.ToQueueId);
        Assert.Equal("Bea Recipient", entry.ToName);
        Assert.Equal("They are asking about the March invoice.", entry.Note);
    }

    [Fact]
    public async Task TransferAsync_OfAPooledConversation_RecordsTheTeamItCameFrom()
    {
        var conversation = CreateQueueConversation(assignedAgentId: null);
        var context = new TestContextBuilder(conversation);
        context.AddQueue(QueueId, "Billing");
        context.AddAgent(RecipientId, "Bea Recipient", queueIds: [QueueId]);

        await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        var entry = Assert.Single(conversation.History);
        Assert.Null(entry.FromAgentId);
        Assert.Equal("Billing", entry.FromName);
    }

    [Fact]
    public async Task TransferAsync_ToAQueue_RecordsTheTeamName()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddQueue(QueueId, "Billing");

        await context.Service.TransferAsync(ToQueue(QueueId), TestContext.Current.CancellationToken);

        var entry = Assert.Single(conversation.History);
        Assert.Null(entry.ToAgentId);
        Assert.Equal(QueueId, entry.ToQueueId);
        Assert.Equal("Billing", entry.ToName);
    }

    [Fact]
    public async Task TransferAsync_TrimsALongNote()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        var request = ToAgent(RecipientId);
        request.Note = new string('x', MessagingConversationTransferService.MaxNoteLength + 50);

        await context.Service.TransferAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(MessagingConversationTransferService.MaxNoteLength, conversation.History[0].Note.Length);
    }

    [Fact]
    public async Task TransferAsync_KeepsTheHistoryBounded_DroppingTheOldestEntries()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);

        for (var i = 0; i < MessagingConversationTransferService.MaxHistoryEntries; i++)
        {
            conversation.History.Add(new MessagingConversationEvent { Note = "old-" + i, OccurredUtc = _now.AddDays(-1) });
        }

        var context = new TestContextBuilder(conversation);

        await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        Assert.Equal(MessagingConversationTransferService.MaxHistoryEntries, conversation.History.Count);
        Assert.Equal("old-1", conversation.History[0].Note);
        Assert.Equal(RecipientId, conversation.History[^1].ToAgentId);
    }

    [Fact]
    public async Task TransferAsync_AnnouncesTheTransferToTheRecipientAndThePreviousHolder()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);

        await context.Service.TransferAsync(ToAgent(RecipientId), TestContext.Current.CancellationToken);

        context.Notifier.Verify(notifier => notifier.ConversationAssignedAsync(
            It.Is<MessagingAssignmentNotification>(notification =>
                notification.ConversationId == ConversationId &&
                notification.IsTransfer &&
                notification.AssignedAgentId == RecipientId &&
                notification.PreviousAgentId == SenderId &&
                notification.TransferredByAgentId == SenderId &&
                notification.TransferredByName == "Ann Sender" &&
                notification.TransferredToName == "Bea Recipient" &&
                notification.OwnerQueueId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransferAsync_ToAQueue_AnnouncesItToTheTeam()
    {
        var conversation = CreatePersonalConversation(holderId: SenderId);
        var context = new TestContextBuilder(conversation);
        context.AddQueue(QueueId, "Billing");

        await context.Service.TransferAsync(ToQueue(QueueId), TestContext.Current.CancellationToken);

        context.Notifier.Verify(notifier => notifier.ConversationAssignedAsync(
            It.Is<MessagingAssignmentNotification>(notification =>
                notification.IsTransfer &&
                notification.AssignedAgentId == null &&
                notification.OwnerQueueId == QueueId &&
                notification.PreviousAgentId == SenderId &&
                notification.TransferredToName == "Billing"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MessagingTransferRequest ToAgent(string agentId)
        => new()
        {
            ConversationId = ConversationId,
            TargetType = ConversationRouteTargetType.Agent,
            TargetId = agentId,
            ActingAgentId = SenderId,
        };

    private static MessagingTransferRequest ToQueue(string queueId)
        => new()
        {
            ConversationId = ConversationId,
            TargetType = ConversationRouteTargetType.Queue,
            TargetId = queueId,
            ActingAgentId = SenderId,
        };

    private static MessagingConversation CreatePersonalConversation(string holderId)
        => new()
        {
            ItemId = ConversationId,
            Channel = "SMS",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = holderId,
            AssignedAgentId = holderId,
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
        };

    private static MessagingConversation CreateQueueConversation(string assignedAgentId)
        => new()
        {
            ItemId = ConversationId,
            Channel = "SMS",
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = QueueId,
            AssignedAgentId = assignedAgentId,
            AssignmentStatus = assignedAgentId is null ? ConversationAssignmentStatus.Pooled : ConversationAssignmentStatus.Assigned,
        };

    private sealed class TestContextBuilder
    {
        private readonly Dictionary<string, AgentProfile> _agents = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ActivityQueue> _queues = new(StringComparer.Ordinal);

        public TestContextBuilder(MessagingConversation conversation, bool allowTransfer = true, bool withQueues = true)
        {
            Store.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(conversation);
            Store.Setup(store => store.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

            Authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<MessagingConversation>(), It.IsAny<ConversationOperation>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(allowTransfer);

            var agents = new Mock<IAgentProfileManager>();
            agents
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id is not null && _agents.TryGetValue(id, out var agent) ? agent : null);

            var names = new Mock<IMessagingAgentNameProvider>();
            names
                .Setup(provider => provider.GetDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id is not null && _names.TryGetValue(id, out var name) ? name : null);

            var queueManagers = new List<IActivityQueueManager>();

            if (withQueues)
            {
                var queues = new Mock<IActivityQueueManager>();
                queues
                    .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string id, CancellationToken _) => id is not null && _queues.TryGetValue(id, out var queue) ? queue : null);
                queueManagers.Add(queues.Object);
            }

            var clock = new Mock<IClock>();
            clock.SetupGet(instance => instance.UtcNow).Returns(_now);

            AddAgent(SenderId, "Ann Sender");
            AddAgent(RecipientId, "Bea Recipient");

            Service = new MessagingConversationTransferService(
                Store.Object,
                Authorization.Object,
                agents.Object,
                queueManagers,
                new PermissiveAgentEntitlementPolicy(),
                names.Object,
                Notifier.Object,
                clock.Object,
                NullLogger<MessagingConversationTransferService>.Instance);
        }

        public Mock<IMessagingConversationStore> Store { get; } = new();

        public Mock<IMessagingConversationAuthorizationService> Authorization { get; } = new();

        public Mock<IMessagingRealTimeNotifier> Notifier { get; } = new();

        public MessagingConversationTransferService Service { get; }

        public void AddAgent(string id, string name, IList<string> queueIds = null)
        {
            _agents[id] = new AgentProfile
            {
                ItemId = id,
                UserId = "user-" + id,
                QueueIds = queueIds ?? [],
            };
            _names[id] = name;
        }

        public void AddQueue(string id, string name, bool enabled = true)
            => _queues[id] = new ActivityQueue { ItemId = id, Name = name, Enabled = enabled };
    }
}
