using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailDeliveryEventProcessorTests
{
    private const string AddressId = "address-1";

    [Fact]
    public async Task ProcessAsync_AHardBounce_SuppressesTheAddressAndMarksTheEmailFailed()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddHours(-1), "gone@example.com", messageId: "m-1@contoso.com");
        harness.SentMessages["m-1@contoso.com"] = new OmnichannelMessage { ServiceAddress = "news@contoso.com", CustomerAddress = "gone@example.com" };

        // Act
        var processed = await harness.Processor.ProcessAsync([Bounce("gone@example.com", "<m-1@contoso.com>", "5.1.1")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, processed);
        Assert.Equal(EmailSuppressionReason.HardBounce, harness.Suppressions.Items["gone@example.com"].Reason);
        Assert.Contains(harness.Log.Entries, entry => entry.Kind == EmailDeliveryEventKind.HardBounce && entry.AddressId == AddressId);
        harness.ConversationService.Verify(service => service.ApplyDeliveryReceiptAsync(
            It.Is<MessageDeliveryReceipt>(receipt => receipt.ProviderMessageId == "m-1@contoso.com" && receipt.Status == MessageDeliveryStatus.Failed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_TheSameEventDeliveredTwice_IsActedOnOnce()
    {
        // Arrange
        var harness = new Harness();
        var complaint = new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "bob@example.com", EventId = "evt-1", Provider = "sendgrid" };

        // Act
        var first = await harness.Processor.ProcessAsync([complaint], TestContext.Current.CancellationToken);
        var second = await harness.Processor.ProcessAsync([complaint], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Single(harness.Log.Entries, entry => entry.Kind == EmailDeliveryEventKind.Complaint);
        harness.OptOut.Verify(service => service.OptOutAsync("bob@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_AnEventDatedOutsideTheLogsRetention_IsLoggedWithinIt()
    {
        // Arrange
        var harness = new Harness();
        var future = new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "fay@example.com", EventId = "evt-future", Provider = "sendgrid", OccurredUtc = harness.Clock.UtcNow.AddYears(5) };
        var ancient = new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "abe@example.com", EventId = "evt-ancient", Provider = "sendgrid", OccurredUtc = harness.Clock.UtcNow.AddYears(-5) };
        var recent = new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "rae@example.com", EventId = "evt-recent", Provider = "sendgrid", OccurredUtc = harness.Clock.UtcNow.AddHours(-2) };

        // Act
        await harness.Processor.ProcessAsync([future, ancient, recent], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(harness.Clock.UtcNow, harness.Log.Entries.Single(entry => entry.Recipient == "fay@example.com").OccurredUtc);
        Assert.Equal(harness.Clock.UtcNow.Subtract(EmailHealthPolicy.DeliveryLogRetention), harness.Log.Entries.Single(entry => entry.Recipient == "abe@example.com").OccurredUtc);
        Assert.Equal(harness.Clock.UtcNow.AddHours(-2), harness.Log.Entries.Single(entry => entry.Recipient == "rae@example.com").OccurredUtc);
    }

    [Fact]
    public void CreateEventId_UsesTheTimeTheProviderSent_SoAFutureDatedEventIsStillRecognised()
    {
        // Arrange
        var deliveryEvent = new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.HardBounce, Recipient = "fay@example.com", Provider = "postmark", OccurredUtc = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        // Act
        var first = EmailDeliveryEventProcessor.CreateEventId(deliveryEvent, "fay@example.com", "m-1@contoso.com");
        var second = EmailDeliveryEventProcessor.CreateEventId(deliveryEvent, "fay@example.com", "m-1@contoso.com");

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(first, EmailDeliveryEventProcessor.CreateEventId(new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.HardBounce, Recipient = "fay@example.com", Provider = "postmark", OccurredUtc = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc) }, "fay@example.com", "m-1@contoso.com"));
    }

    [Fact]
    public async Task InboxHandler_ARedeliveredBatch_ChangesNothingTheSecondTime()
    {
        // Arrange
        var harness = new Harness();
        var handler = new EmailDeliveryEventsInboxHandler(harness.Processor);
        var payload = JsonSerializer.Serialize(new[]
        {
            Bounce("gone@example.com", null, "5.1.1"),
            new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "bob@example.com", Provider = "ses", EventId = "fb-1:0" },
        });

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);
        var entriesAfterFirst = harness.Log.Entries.Count;
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, entriesAfterFirst);
        Assert.Equal(entriesAfterFirst, harness.Log.Entries.Count);
        Assert.Equal(2, harness.Suppressions.Items.Count);
        harness.OptOut.Verify(service => service.OptOutAsync("bob@example.com", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Webhooks.WebhookHandlerReplaySafety.GuardedByDurableStore, handler.ReplaySafety);
    }

    [Fact]
    public async Task ProcessAsync_AComplaint_SuppressesOptsOutAndChecksTheSendersHealth()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddHours(-2), "bob@example.com");

        // Act
        await harness.Processor.ProcessAsync([new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Complaint, Recipient = "Bob@Example.com", Provider = "postmark" }], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailSuppressionReason.Complaint, harness.Suppressions.Items["bob@example.com"].Reason);
        harness.OptOut.Verify(service => service.OptOutAsync("bob@example.com", It.IsAny<CancellationToken>()), Times.Once);
        harness.Governor.Verify(governor => governor.CheckHealthAsync(It.Is<OmnichannelChannelEndpoint>(address => address.ItemId == AddressId), It.IsAny<EmailAddressSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ThirdEmailToSoftBounce_SuppressesTheAddressAndNotesTheDomainDeferral()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddDays(-3), "full@example.com");

        // Act
        for (var i = 0; i < 3; i++)
        {
            await harness.Processor.ProcessAsync(
                [new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.SoftBounce, Recipient = "full@example.com", MessageId = $"m-{i}@contoso.com", Status = "5.2.2", Reason = "Mailbox full", EventId = $"soft-{i}", Provider = "ses" }],
                TestContext.Current.CancellationToken);

            if (i < 2)
            {
                Assert.Empty(harness.Suppressions.Items);
            }
        }

        // Assert
        Assert.Equal(EmailSuppressionReason.RepeatedSoftBounces, harness.Suppressions.Items["full@example.com"].Reason);
        harness.Governor.Verify(governor => governor.NoteDomainDeferralAsync(AddressId, "example.com", It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    // Live: a greylisting server deferred one email three times, the three reports counted as three soft bounces, and a
    // good address was suppressed.
    [Fact]
    public async Task ProcessAsync_OneEmailSoftBouncingThreeTimes_DoesNotSuppressTheAddress()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddHours(-3), "ann@example.com");

        // Act
        for (var i = 0; i < 3; i++)
        {
            await harness.Processor.ProcessAsync(
                [new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.SoftBounce, Recipient = "ann@example.com", MessageId = "m-1@contoso.com", Status = "5.2.2", EventId = $"soft-{i}", Provider = "postmark" }],
                TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(harness.Suppressions.Items);
    }

    [Fact]
    public async Task ProcessAsync_ATemporaryFailureReportedAsASoftBounce_IsADeferral()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddHours(-3), "ann@example.com");

        // Act
        for (var i = 0; i < 3; i++)
        {
            await harness.Processor.ProcessAsync(
                [new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.SoftBounce, Recipient = "ann@example.com", MessageId = $"m-{i}@contoso.com", Status = "4.7.1", Reason = "Greylisted, try again later", EventId = $"grey-{i}", Provider = "postmark" }],
                TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(harness.Suppressions.Items);
        Assert.Equal(3, harness.Log.Entries.Count(entry => entry.Kind == EmailDeliveryEventKind.Deferred));
        harness.Governor.Verify(governor => governor.NoteDomainDeferralAsync(AddressId, "example.com", It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessAsync_ADomainThatDoesNotExist_SuppressesTheAddressWithoutPausingTheSender()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddMinutes(-5), "ann@nowhere-example.com");

        // Act
        await harness.Processor.ProcessAsync([Bounce("ann@nowhere-example.com", null, "5.4.4")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailSuppressionReason.HardBounce, harness.Suppressions.Items["ann@nowhere-example.com"].Reason);
        harness.Governor.Verify(governor => governor.PauseAsync(It.IsAny<string>(), It.IsAny<EmailSendingPauseKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_AnEmailTooLarge_FailsTheEmailWithoutSuppressingOrPausing()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddMinutes(-5), "ann@example.com", messageId: "m-2@contoso.com");
        harness.SentMessages["m-2@contoso.com"] = new OmnichannelMessage { ServiceAddress = "news@contoso.com", CustomerAddress = "ann@example.com" };

        // Act
        await harness.Processor.ProcessAsync([Bounce("ann@example.com", "m-2@contoso.com", "5.3.4")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Suppressions.Items);
        harness.Governor.Verify(governor => governor.PauseAsync(It.IsAny<string>(), It.IsAny<EmailSendingPauseKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.Governor.Verify(governor => governor.NoteDomainDeferralAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.ConversationService.Verify(service => service.ApplyDeliveryReceiptAsync(
            It.Is<MessageDeliveryReceipt>(receipt => receipt.ProviderMessageId == "m-2@contoso.com" && receipt.Status == MessageDeliveryStatus.Failed),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.4.4", "Host or domain name not found", EmailDeliveryEventKind.HardBounce, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.0.0", null, EmailDeliveryEventKind.HardBounce, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.5.0", "Requested action not taken: mailbox unavailable", EmailDeliveryEventKind.HardBounce, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.5.0", "Requested action not taken", EmailDeliveryEventKind.Rejected, true)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.3.4", "Message size exceeds fixed limit", EmailDeliveryEventKind.Rejected, true)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.6.0", "Content rejected", EmailDeliveryEventKind.Rejected, true)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.7.1", "Blocked by policy", EmailDeliveryEventKind.Blocked, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "5.2.2", "Mailbox full", EmailDeliveryEventKind.SoftBounce, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, "4.4.7", "Message expired", EmailDeliveryEventKind.SoftBounce, false)]
    [InlineData(EmailDeliveryEventKind.SoftBounce, "4.2.2", "The email account is over quota", EmailDeliveryEventKind.Deferred, false)]
    [InlineData(EmailDeliveryEventKind.SoftBounce, null, "smtp; 421 4.7.0 Try again later", EmailDeliveryEventKind.Deferred, false)]
    [InlineData(EmailDeliveryEventKind.SoftBounce, null, "Mailbox full", EmailDeliveryEventKind.SoftBounce, false)]
    [InlineData(EmailDeliveryEventKind.HardBounce, null, "Unknown user", EmailDeliveryEventKind.HardBounce, false)]
    public void Reclassify_ReadsTheStatusCodeAndTheProvidersWords(EmailDeliveryEventKind reported, string status, string reason, EmailDeliveryEventKind expected, bool isRejectedEmail)
    {
        // Act
        var result = EmailDeliveryEventProcessor.Reclassify(new EmailDeliveryEvent { Kind = reported, Status = status, Reason = reason, Provider = "sendgrid" });

        // Assert
        Assert.Equal(expected, result.Kind);
        Assert.Equal(isRejectedEmail, result.IsRejectedEmail);
    }

    [Fact]
    public async Task ProcessAsync_ABounceThatIsReallyABlock_PausesTheSenderAndKeepsTheRecipient()
    {
        // Arrange
        var harness = new Harness();
        harness.Log.AddSent(AddressId, harness.Clock.UtcNow.AddMinutes(-5), "ann@example.com");

        // Act
        await harness.Processor.ProcessAsync([Bounce("ann@example.com", null, "5.7.1")], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Suppressions.Items);
        harness.Governor.Verify(governor => governor.PauseAsync(AddressId, EmailSendingPauseKind.Blocked, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ABounceReportWithoutARecipient_TakesItFromTheBouncedEmail()
    {
        // Arrange
        var harness = new Harness();
        harness.SentMessages["m-9@contoso.com"] = new OmnichannelMessage { ServiceAddress = "news@contoso.com", CustomerAddress = "Gone@Example.com" };

        // Act
        await harness.Processor.ProcessAsync([Bounce(null, "m-9@contoso.com", "5.1.1")], TestContext.Current.CancellationToken);

        // Assert
        Assert.True(harness.Suppressions.Items.ContainsKey("gone@example.com"));
    }

    [Fact]
    public async Task ProcessAsync_AnUnsubscribe_OptsTheAddressOutWithoutSuppressingIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.Processor.ProcessAsync([new EmailDeliveryEvent { Kind = EmailDeliveryEventKind.Unsubscribed, Recipient = "cy@example.com", Provider = "sendgrid" }], TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.Suppressions.Items);
        harness.OptOut.Verify(service => service.OptOutAsync("cy@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EmailDeliveryEvent Bounce(string recipient, string messageId, string status)
        => new()
        {
            Kind = EmailDeliveryEventKind.HardBounce,
            Recipient = recipient,
            MessageId = messageId,
            Status = status,
            Reason = $"smtp; 550 {status}",
            Provider = "sendgrid",
            OccurredUtc = new DateTime(2026, 10, 9, 11, 0, 0, DateTimeKind.Utc),
        };

    private sealed class Harness
    {
        public Harness()
        {
            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(manager => manager.FindByIdAsync(AddressId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelChannelEndpoint { ItemId = AddressId, Value = "news@contoso.com", Capabilities = [OmnichannelConstants.Channels.Email] });

            var finder = new Mock<IEmailSentMessageFinder>();
            finder
                .Setup(value => value.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => SentMessages.GetValueOrDefault(id ?? string.Empty));

            Processor = new EmailDeliveryEventProcessor(
                Log,
                Suppressions,
                Governor.Object,
                OptOut.Object,
                endpointManager.Object,
                ConversationService.Object,
                finder.Object,
                Clock,
                NullLogger<EmailDeliveryEventProcessor>.Instance);
        }

        public InMemoryEmailDeliveryLog Log { get; } = new();

        public InMemoryEmailSuppressionList Suppressions { get; } = new();

        public Mock<IEmailSendingGovernor> Governor { get; } = new();

        public Mock<IEmailOptOutService> OptOut { get; } = new();

        public Mock<IMessagingConversationService> ConversationService { get; } = new();

        public Dictionary<string, OmnichannelMessage> SentMessages { get; } = new(StringComparer.Ordinal);

        public DeliverabilityClock Clock { get; } = new();

        public EmailDeliveryEventProcessor Processor { get; }
    }
}
