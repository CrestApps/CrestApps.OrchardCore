using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// The dispatcher is the one door every email leaves through, so it is where the suppression list, the sending limits
/// and the reading of the server's refusals meet the send.
/// </summary>
public sealed class EmailDispatcherDeliverabilityTests
{
    [Fact]
    public async Task SendAsync_ToASuppressedAddress_IsRefusedWithoutReachingTheServer()
    {
        // Arrange
        var harness = new Harness();
        await harness.Suppressions.SuppressAsync("gone@example.com", EmailSuppressionReason.HardBounce, "5.1.1", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await harness.Dispatcher.SendAsync(Message("gone@example.com", MessagingOutboundPurpose.Broadcast), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(OmnichannelConstants.MessagingErrorCodes.RecipientRejected, result.ErrorCode);
        Assert.Empty(harness.Transport.Sent);
    }

    [Fact]
    public async Task SendAsync_BulkMailPastTheLimit_IsHeldBackNotFailed()
    {
        // Arrange
        var harness = new Harness(limits => limits.MaxPerHour = 1);
        harness.Log.AddSent(Harness.AddressId, harness.Clock.UtcNow.AddMinutes(-10));

        // Act
        var result = await harness.Dispatcher.SendAsync(Message("ann@example.com", MessagingOutboundPurpose.Broadcast), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsDeferred);
        Assert.Equal(OmnichannelConstants.MessagingErrorCodes.Deferred, result.ErrorCode);
        Assert.Equal(harness.Clock.UtcNow.AddMinutes(50), result.RetryAfterUtc);
        Assert.Empty(harness.Transport.Sent);
    }

    [Fact]
    public async Task SendAsync_AnAcceptedEmail_IsLoggedUnderItsMessageId()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await harness.Dispatcher.SendAsync(Message("ann@example.com", MessagingOutboundPurpose.Outreach), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var entry = Assert.Single(harness.Log.Entries);
        Assert.Equal(EmailDeliveryEventKind.Sent, entry.Kind);
        Assert.Equal("ann@example.com", entry.Recipient);
        Assert.Equal("example.com", entry.RecipientDomain);
        Assert.True(entry.IsBulk);
        Assert.Equal(Assert.Single(harness.Transport.Sent).MessageId, entry.MessageId);
    }

    [Fact]
    public async Task SendAsync_ARecipientTheServerDoesNotKnow_IsSuppressedAndNotRetried()
    {
        // Arrange
        var harness = new Harness();
        harness.Transport.NextResult = new MessageDispatchResult
        {
            Succeeded = false,
            ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected,
            Errors = [new LocalizedString("x", "The mail server refused the recipient (550): 5.1.1 User unknown")],
        };

        // Act
        var result = await harness.Dispatcher.SendAsync(Message("gone@example.com", MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OmnichannelConstants.MessagingErrorCodes.RecipientRejected, result.ErrorCode);
        Assert.Equal(EmailSuppressionReason.HardBounce, harness.Suppressions.Items["gone@example.com"].Reason);
    }

    [Fact]
    public async Task SendAsync_APolicyRefusal_PausesBulkMailAndKeepsTheRecipient()
    {
        // Arrange
        var harness = new Harness();
        harness.Transport.NextResult = new MessageDispatchResult
        {
            Succeeded = false,
            ErrorCode = OmnichannelConstants.MessagingErrorCodes.RecipientRejected,
            Errors = [new LocalizedString("x", "The mail server refused the recipient (550): 5.7.1 Message rejected due to the sender's reputation")],
        };

        // Act
        var result = await harness.Dispatcher.SendAsync(Message("ann@example.com", MessagingOutboundPurpose.Broadcast), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OmnichannelConstants.MessagingErrorCodes.SenderBlocked, result.ErrorCode);
        Assert.Empty(harness.Suppressions.Items);
        Assert.Equal(EmailSendingPauseKind.Blocked, harness.States.States[Harness.AddressId].PauseKind);
    }

    [Fact]
    public async Task SendAsync_AThrottledBroadcast_WaitsForThePauseInsteadOfSpendingARetry()
    {
        // Arrange
        var harness = new Harness();
        harness.Transport.NextResult = MessageDispatchResult.Failed("The mail server did not accept the email (421): 4.7.0 Too many messages, slow down");

        // Act
        var bulk = await harness.Dispatcher.SendAsync(Message("ann@example.com", MessagingOutboundPurpose.Broadcast), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(bulk.IsDeferred);
        Assert.Equal(harness.Clock.UtcNow.AddMinutes(15), bulk.RetryAfterUtc);
    }

    [Fact]
    public async Task SendAsync_AThrottledReply_IsAnOrdinaryRetryAndStillPausesBulkMail()
    {
        // Arrange
        var harness = new Harness();
        harness.Transport.NextResult = MessageDispatchResult.Failed("The mail server did not accept the email (421): 4.7.0 Too many messages, slow down");

        // Act
        var reply = await harness.Dispatcher.SendAsync(Message("ann@example.com", MessagingOutboundPurpose.Reply), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(reply.IsDeferred);
        Assert.False(reply.Succeeded);
        Assert.Equal(EmailSendingPauseKind.Throttled, harness.States.States[Harness.AddressId].PauseKind);
    }

    private static MessagingOutboundMessage Message(string recipient, MessagingOutboundPurpose purpose)
        => new()
        {
            ServiceAddress = "news@contoso.com",
            ContactAddress = recipient,
            Purpose = purpose,
            Subject = "October news",
            Body = "Hello",
        };

    private sealed class Harness
    {
        public const string AddressId = "address-news";

        public Harness(Action<EmailSendingLimits> configureLimits = null)
        {
            var address = new OmnichannelChannelEndpoint
            {
                ItemId = AddressId,
                Value = "news@contoso.com",
                DisplayText = "Contoso News",
                Capabilities = [OmnichannelConstants.Channels.Email],
            };

            var settings = new EmailAddressSettings
            {
                TransportName = FailingTransport.TransportName,
                IncludeUnsubscribeLink = false,
                Limits = new EmailSendingLimits { MinimumSecondsBetweenSends = 0 },
            };

            configureLimits?.Invoke(settings.Limits);
            address.Put(settings);

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(manager => manager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Email, "news@contoso.com", It.IsAny<CancellationToken>()))
                .ReturnsAsync(address);

            var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out var suppressions, out var states, out var clock);

            Log = log;
            Suppressions = suppressions;
            States = states;
            Clock = clock;

            Dispatcher = new EmailDispatcher(
                endpointManager.Object,
                [Transport],
                Mock.Of<IMessagingAttachmentStore>(),
                Mock.Of<IEmailUnsubscribeLinks>(),
                governor,
                Mock.Of<ISession>(),
                NullLogger<EmailDispatcher>.Instance,
                new PassThroughStringLocalizer<EmailDispatcher>());
        }

        public FailingTransport Transport { get; } = new();

        public InMemoryEmailDeliveryLog Log { get; }

        public InMemoryEmailSuppressionList Suppressions { get; }

        public InMemoryEmailSendingStateStore States { get; }

        public DeliverabilityClock Clock { get; }

        public EmailDispatcher Dispatcher { get; }
    }

    private sealed class FailingTransport : IEmailTransport
    {
        public const string TransportName = "Failing";

        public List<EmailTransportMessage> Sent { get; } = [];

        public MessageDispatchResult NextResult { get; set; }

        public string Name => TransportName;

        public LocalizedString DisplayName => new(TransportName, TransportName);

        public bool SupportsHeaders => true;

        public Task<MessageDispatchResult> SendAsync(EmailTransportMessage message, EmailAddressSettings settings, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);

            return Task.FromResult(NextResult ?? MessageDispatchResult.Success(message.MessageId));
        }
    }
}
