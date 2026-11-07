using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Outreach.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Outreach;

/// <summary>
/// Broadcasts are sent by the outreach engine: each recipient once, in their own thread, opted-out recipients skipped,
/// a broadcast half sent before the engine resumed without re-sending, and the run's progress shown on the broadcast.
/// </summary>
public sealed class MessagingBroadcastOutreachTests
{
    [Fact]
    public async Task AQueuedBroadcast_IsSentToEachRecipientOnce_AndCompletes()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001", "+15551110002", "+15551110003"]);
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(["+15551110001", "+15551110002", "+15551110003"], sentTo);
        Assert.Equal((3, 0, 0, MessagingBroadcastStatus.Completed), (broadcast.SentCount, broadcast.FailedCount, broadcast.SkippedCount, broadcast.Status));
        Assert.NotNull(broadcast.CompletedUtc);
        Assert.False(string.IsNullOrEmpty(broadcast.OutreachRunId));
    }

    [Fact]
    public async Task ALargeBroadcast_IsSentAPageAtATime()
    {
        // Arrange
        var recipients = Enumerable.Range(0, 120).Select(index => $"+1555111{index:0000}").ToList();
        var broadcast = NewBroadcast(recipients);
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(recipients, sentTo);
        Assert.Equal(3, (await harness.GetBatchesAsync(broadcast.OutreachRunId)).Count);
        Assert.Equal(120, broadcast.SentCount);
    }

    [Fact]
    public async Task ABroadcastHalfSentBeforeTheEngine_SkipsTheRecipientsItAlreadyProcessed()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001", "+15551110002", "+15551110003"]);
        broadcast.ProcessedRecipients = ["+15551110001", "+15551110002"];
        broadcast.SentCount = 2;
        broadcast.Status = MessagingBroadcastStatus.Running;
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(["+15551110003"], sentTo);
        Assert.Equal((3, MessagingBroadcastStatus.Completed), (broadcast.SentCount, broadcast.Status));
    }

    [Fact]
    public async Task ABroadcastMidSendOnUpgrade_ResumesWithoutReSendingOrSkipping_AndKeepsItsCounts()
    {
        // Arrange: a broadcast the old fan-out stopped after 60 of 120 recipients (a whole first page and part of the
        // second), 58 sent and 2 failed.
        var recipients = Enumerable.Range(0, 120).Select(index => $"+1555222{index:0000}").ToList();
        var broadcast = NewBroadcast(recipients);
        broadcast.ProcessedRecipients = [.. recipients.Take(60)];
        broadcast.SentCount = 58;
        broadcast.FailedCount = 2;
        broadcast.Status = MessagingBroadcastStatus.Running;
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act: the background sweep picks the running broadcast up.
        await ProcessPendingAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(recipients.Skip(60), sentTo);
        Assert.Equal((118, 2, MessagingBroadcastStatus.Completed), (broadcast.SentCount, broadcast.FailedCount, broadcast.Status));
    }

    [Fact]
    public async Task AQueuedBroadcast_IsStartedByTheSweep_AndSent()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001", "+15551110002"]);
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act
        await ProcessPendingAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(["+15551110001", "+15551110002"], sentTo);
        Assert.Equal(MessagingBroadcastStatus.Completed, broadcast.Status);
    }

    [Fact]
    public async Task ARecipientListedTwice_IsSentOnce_EvenAcrossPages()
    {
        // Arrange: the old fan-out sent a number listed twice only once.
        var recipients = Enumerable.Range(0, 60).Select(index => $"+1555333{index:0000}").ToList();
        recipients.Add(recipients[5]);
        recipients.Add(recipients[55].ToLowerInvariant());
        var broadcast = NewBroadcast(recipients);
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo);

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(recipients.Take(60), sentTo);
        Assert.Equal(60, broadcast.SentCount);
    }

    [Fact]
    public async Task ARecipientWhoOptedOut_IsSkipped_NotSent()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001", "+15551110002"]);
        var sentTo = new List<string>();
        var consent = new Mock<IConsentService>();
        consent
            .Setup(service => service.GetAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) =>
                query is { Channel: ConsentChannels.Sms, Address: "+15551110002", ContactKind: ConsentContactKinds.Contact }
                    ? ConsentStatus.OptedOutBy("contact-preferences")
                    : ConsentStatus.NotOptedOut);
        await using var harness = await CreateAsync(broadcast, sentTo, consent: consent.Object);

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal(["+15551110001"], sentTo);
        Assert.Equal((1, 0, 1), (broadcast.SentCount, broadcast.FailedCount, broadcast.SkippedCount));
    }

    [Fact]
    public async Task ARefusedSend_IsCountedAsFailed()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001", "+15551110002"]);
        var sentTo = new List<string>();
        await using var harness = await CreateAsync(broadcast, sentTo, refuse: "+15551110002");

        // Act
        await StartAsync(harness, broadcast);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal((1, 1, MessagingBroadcastStatus.Completed), (broadcast.SentCount, broadcast.FailedCount, broadcast.Status));
    }

    [Fact]
    public async Task ABroadcastWithoutABody_Fails_WithoutARun()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001"]);
        broadcast.Body = " ";
        await using var harness = await CreateAsync(broadcast, []);

        // Act
        await StartAsync(harness, broadcast);

        // Assert
        Assert.Equal(MessagingBroadcastStatus.Failed, broadcast.Status);
        Assert.Null(broadcast.OutreachRunId);
    }

    [Fact]
    public async Task ABroadcastAlreadyOnTheEngine_IsNotStartedTwice()
    {
        // Arrange
        var broadcast = NewBroadcast(["+15551110001"]);
        await using var harness = await CreateAsync(broadcast, []);
        await StartAsync(harness, broadcast);
        var runId = broadcast.OutreachRunId;

        // Act
        await StartAsync(harness, broadcast);

        // Assert
        Assert.Equal(runId, broadcast.OutreachRunId);
    }

    [Fact]
    public void TheMessagingSender_ObservesQuietHoursOnlyWhereTheChannelDoes()
    {
        // Arrange
        var sms = Channel(observesQuietHours: true);
        var chat = Channel(observesQuietHours: false);
        var resolver = new Mock<IMessagingChannelResolver>();
        resolver.Setup(value => value.Get("SMS")).Returns(sms);
        resolver.Setup(value => value.Get("Chat")).Returns(chat);
        var sender = new MessagingOutreachChannelSender(Mock.Of<IMessagingConversationService>(), resolver.Object);

        // Act / Assert
        Assert.True(sender.ObservesQuietHours(new OutreachRun { Channel = "SMS" }));
        Assert.False(sender.ObservesQuietHours(new OutreachRun { Channel = "Chat" }));
        Assert.False(sender.ObservesQuietHours(new OutreachRun { Channel = "Unknown" }));
    }

    private static MessagingBroadcast NewBroadcast(List<string> recipients)
        => new()
        {
            ItemId = "bc-1",
            Name = "Holiday hours",
            Channel = "SMS",
            ServiceAddress = "+15553334444",
            Body = "We are closed on Friday.",
            Recipients = recipients,
            OwnerAgentId = "agent-1",
            Status = MessagingBroadcastStatus.Queued,
        };

    private static async Task StartAsync(OutreachTestHarness harness, MessagingBroadcast broadcast)
    {
        await using var scope = harness.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMessagingBroadcastService>().ProcessAsync(broadcast, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ProcessPendingAsync(OutreachTestHarness harness, MessagingBroadcast broadcast)
    {
        await using var scope = harness.Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IMessagingBroadcastService>().ProcessPendingAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(broadcast.OutreachRunId));
    }

    private static Task<OutreachTestHarness> CreateAsync(MessagingBroadcast broadcast, List<string> sentTo, IConsentService consent = null, string refuse = null)
    {
        var store = new Mock<IMessagingBroadcastStore>();
        store.Setup(value => value.FindByIdAsync(broadcast.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(broadcast);
        store
            .Setup(value => value.GetByStatusAsync(It.IsAny<MessagingBroadcastStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessagingBroadcastStatus status, CancellationToken _) => broadcast.Status == status ? new[] { broadcast } : Array.Empty<MessagingBroadcast>());
        store.Setup(value => value.UpdateAsync(It.IsAny<MessagingBroadcast>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var conversations = new Mock<IMessagingConversationService>();
        conversations
            .Setup(value => value.SendDirectAsync("SMS", "+15553334444", It.IsAny<string>(), "We are closed on Friday.", "agent-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string to, string _, string _, CancellationToken _) =>
            {
                if (to == refuse)
                {
                    return MessagingSendResult.Failed("blocked");
                }

                sentTo.Add(to);

                return new MessagingSendResult { Succeeded = true };
            });

        var channels = new Mock<IMessagingChannelResolver>();
        channels.Setup(value => value.Get("SMS")).Returns(Channel(observesQuietHours: true));

        return OutreachTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(store.Object);
            services.AddSingleton(conversations.Object);
            services.AddSingleton(channels.Object);

            if (consent is not null)
            {
                services.AddSingleton(consent);
            }

            services.AddScoped<IMessagingBroadcastService, MessagingBroadcastService>();
            services
                .AddOutreachAudienceSource<MessagingBroadcastAudienceSource>()
                .AddOutreachChannelSender<MessagingOutreachChannelSender>()
                .AddOutreachRunObserver<MessagingBroadcastRunObserver>();
        });
    }

    private static IMessagingChannel Channel(bool observesQuietHours)
    {
        var channel = new Mock<IMessagingChannel>();
        channel.SetupGet(value => value.Capabilities).Returns(new MessagingChannelCapabilities { ObservesQuietHours = observesQuietHours });

        return channel.Object;
    }
}
