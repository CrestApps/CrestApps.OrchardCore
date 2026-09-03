using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class SmsAgentHandoffServiceTests
{
    [Fact]
    public void CanHandle_OnlySms()
    {
        var harness = new Harness();

        Assert.True(harness.Service.CanHandle("SMS"));
        Assert.True(harness.Service.CanHandle("sms"));
        Assert.False(harness.Service.CanHandle("Phone"));
    }

    [Fact]
    public async Task RequestHandoff_CreatesQueueOwnedConversation_ImportsTranscript_AndNotifies()
    {
        var harness = new Harness();

        var request = new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1", AISessionId = "sess-1", ContactContentItemId = "contact-1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Transcript =
            [
                new OmnichannelHandoffMessage { IsInbound = false, Content = "Hi, are you still shopping?", CreatedUtc = DateTime.UtcNow.AddMinutes(-5) },
                new OmnichannelHandoffMessage { IsInbound = true, Content = "Yes, can I talk to a person?", CreatedUtc = DateTime.UtcNow.AddMinutes(-4) },
                new OmnichannelHandoffMessage { IsInbound = false, Content = "Connecting you now.", CreatedUtc = DateTime.UtcNow.AddMinutes(-3) },
            ],
        };

        var result = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        var conversation = harness.CreatedConversation;
        Assert.NotNull(conversation);
        Assert.Equal(result.ConversationId, conversation.ItemId);
        Assert.Equal(SmsConversationOwnerType.Queue, conversation.OwnerType);
        Assert.Equal("queue-1", conversation.OwnerId);
        Assert.Equal(SmsConversationAssignmentStatus.Unassigned, conversation.AssignmentStatus);
        Assert.Null(conversation.AssignedAgentId);
        Assert.Equal(SmsConversationStatus.Open, conversation.Status);
        Assert.Equal("sess-1", conversation.AISessionId);
        Assert.Equal("contact-1", conversation.ContactContentItemId);
        Assert.False(conversation.IsRead);
        Assert.Equal(3, conversation.UnreadCount);
        Assert.Equal("Connecting you now.", conversation.LastMessagePreview);

        // The three transcript turns were imported as linked messages.
        Assert.Equal(3, harness.SavedMessages.Count);
        Assert.All(harness.SavedMessages, m => Assert.Equal(conversation.ItemId, m.ConversationId));
        Assert.Contains(harness.SavedMessages, m => m.IsInbound && m.Content == "Yes, can I talk to a person?");
        Assert.Contains(harness.SavedMessages, m => !m.IsInbound && m.Content == "Hi, are you still shopping?");

        harness.Notifier.Verify(n => n.NewInboundMessageAsync(
            It.Is<SmsInboundNotification>(x => x.ConversationId == conversation.ItemId && x.OwnerQueueId == "queue-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestHandoff_StoresSummary_OnTheConversation()
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1", AISessionId = "s1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Summary = "Customer wants a refund on a damaged order and asked for a person.",
            Reason = "customer asked for a human",
        }, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Customer wants a refund on a damaged order and asked for a person.", harness.CreatedConversation.Summary);
    }

    [Fact]
    public async Task RequestHandoff_WithoutSummary_FallsBackToReason()
    {
        var harness = new Harness();

        await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            Summary = null,
            Reason = "customer asked for a human",
        }, TestContext.Current.CancellationToken);

        Assert.Equal("customer asked for a human", harness.CreatedConversation.Summary);
    }

    [Fact]
    public async Task RequestHandoff_WithExistingThread_ReRoutesToQueue_WithoutCreating()
    {
        var existing = new SmsConversation
        {
            ItemId = "conv-existing",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            OwnerId = "agent-owner",
            AssignedAgentId = "agent-owner",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
            Status = SmsConversationStatus.Closed,
            UnreadCount = 1,
        };

        var harness = new Harness(existing);

        var request = new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1", AISessionId = "sess-9" },
            TargetQueueId = "queue-2",
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
        };

        var result = await harness.Service.RequestHandoffAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("conv-existing", result.ConversationId);
        Assert.Null(harness.CreatedConversation);
        Assert.Same(existing, harness.UpdatedConversation);
        Assert.Equal(SmsConversationOwnerType.Queue, existing.OwnerType);
        Assert.Equal("queue-2", existing.OwnerId);
        Assert.Equal(SmsConversationAssignmentStatus.Unassigned, existing.AssignmentStatus);
        Assert.Null(existing.AssignedAgentId);
        // A closed thread is re-opened for the queue.
        Assert.Equal(SmsConversationStatus.Open, existing.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestHandoff_WithoutQueue_Fails(string queueId)
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1" },
            TargetQueueId = queueId,
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(harness.CreatedConversation);
    }

    [Fact]
    public async Task RequestHandoff_WithoutAddresses_Fails()
    {
        var harness = new Harness();

        var result = await harness.Service.RequestHandoffAsync(new OmnichannelHandoffRequest
        {
            Activity = new OmnichannelActivity { ItemId = "act1" },
            TargetQueueId = "queue-1",
            ServiceAddress = null,
            ContactAddress = null,
        }, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(harness.CreatedConversation);
    }

    private sealed class Harness
    {
        public Mock<ISmsRealTimeNotifier> Notifier { get; } = new();

        public SmsConversation CreatedConversation { get; private set; }

        public SmsConversation UpdatedConversation { get; private set; }

        public List<OmnichannelMessage> SavedMessages { get; } = [];

        public SmsAgentHandoffService Service { get; }

        public Harness(SmsConversation existing = null)
        {
            var conversationStore = new Mock<ISmsConversationStore>();
            conversationStore.Setup(s => s.FindByAddressesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            conversationStore.Setup(s => s.CreateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<SmsConversation, CancellationToken>((c, _) => CreatedConversation = c);
            conversationStore.Setup(s => s.UpdateAsync(It.IsAny<SmsConversation>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask)
                .Callback<SmsConversation, CancellationToken>((c, _) => UpdatedConversation = c);

            var session = new Mock<ISession>();
            session.Setup(s => s.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback<object, bool, string, CancellationToken>((m, _, _, _) => SavedMessages.Add((OmnichannelMessage)m));

            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            Service = new SmsAgentHandoffService(
                conversationStore.Object,
                Notifier.Object,
                session.Object,
                clock.Object,
                NullLogger<SmsAgentHandoffService>.Instance);
        }
    }
}
