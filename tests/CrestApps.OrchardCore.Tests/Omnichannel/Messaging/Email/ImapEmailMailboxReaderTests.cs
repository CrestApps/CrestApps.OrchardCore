using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using MailKit;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class ImapEmailMailboxReaderTests
{
    private static readonly OmnichannelChannelEndpoint _address = new()
    {
        ItemId = "address-support",
        Value = "support@contoso.com",
    };

    [Fact]
    public async Task ReceiveBatchAsync_AnEmailAboveTheSizeLimit_IsPassedOverWithoutBeingDownloaded()
    {
        // Arrange
        var harness = new Harness();
        harness.Sizes[2] = (uint)EmailChannelConstants.MaxInboundEmailBytes + 1;

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert
        harness.Folder.Verify(folder => folder.GetMessageAsync(It.Is<MailKit.UniqueId>(uid => uid.Id == 2), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()), Times.Never);
        Assert.Equal(new[] { "m1@example.com", "m3@example.com" }, harness.Received);
        Assert.Equal(2, harness.Result.Received);
        Assert.Equal(3u, harness.State.LastUid);
    }

    [Fact]
    public async Task ReceiveBatchAsync_AnEmailThatCannotBeRead_IsPassedOverAndTheRestAreReceived()
    {
        // Arrange
        var harness = new Harness();
        harness.Folder
            .Setup(folder => folder.GetMessageAsync(It.Is<MailKit.UniqueId>(uid => uid.Id == 2), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new FormatException("Not a message."));

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert
        Assert.Equal(new[] { "m1@example.com", "m3@example.com" }, harness.Received);
        Assert.Equal(3u, harness.State.LastUid);
    }

    [Fact]
    public async Task ReceiveBatchAsync_AnEmailTheReceiverFailsOn_IsPassedOverSoItCannotHoldUpTheMailbox()
    {
        // Arrange
        var harness = new Harness();
        harness.FailingMessageId = "m2@example.com";

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert: the read moves past it, so the next read does not try it again forever.
        Assert.Equal(new[] { "m1@example.com", "m2@example.com", "m3@example.com" }, harness.Received);
        Assert.Equal(2, harness.Result.Received);
        Assert.Equal(3u, harness.State.LastUid);
    }

    [Fact]
    public async Task ReceiveBatchAsync_WhenTheConnectionDrops_EndsTheReadWithoutMovingPastTheEmail()
    {
        // Arrange
        var harness = new Harness();
        harness.Folder
            .Setup(folder => folder.GetMessageAsync(It.Is<MailKit.UniqueId>(uid => uid.Id == 2), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
            .ThrowsAsync(new IOException("The connection was closed."));

        // Act & Assert: the email is not at fault, so the next read starts again from it.
        await Assert.ThrowsAsync<IOException>(() => harness.ReceiveAsync(1, 2, 3));

        Assert.Equal(new[] { "m1@example.com" }, harness.Received);
        Assert.Equal(1u, harness.State.LastUid);
    }

    [Fact]
    public async Task ReceiveBatchAsync_WhenTheReceiverIsBusy_StopsAndStartsAgainFromThatEmailNextTime()
    {
        // Arrange
        var harness = new Harness();
        harness.BusyMessageId = "m2@example.com";

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert
        Assert.Equal(new[] { "m1@example.com", "m2@example.com" }, harness.Received);
        Assert.Equal(1, harness.Result.Received);
        Assert.Equal(1u, harness.State.LastUid);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Folder
                .Setup(folder => folder.FetchAsync(It.IsAny<IList<MailKit.UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IList<MailKit.UniqueId> uids, IFetchRequest _, CancellationToken _) => uids.Select(Summary).ToList());

            Folder
                .Setup(folder => folder.GetMessageAsync(It.IsAny<MailKit.UniqueId>(), It.IsAny<CancellationToken>(), It.IsAny<ITransferProgress>()))
                .ReturnsAsync((MailKit.UniqueId uid, CancellationToken _, ITransferProgress _) => Message(uid.Id));

            var receiver = new Mock<IEmailInboundReceiver>();
            receiver
                .Setup(value => value.ReceiveAsync(It.IsAny<InboundEmail>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((InboundEmail email, string _, bool _, CancellationToken _) =>
                {
                    Received.Add(email.MessageId);

                    if (email.MessageId == FailingMessageId)
                    {
                        throw new InvalidOperationException("The receiver failed.");
                    }

                    return new EmailInboundResult
                    {
                        Status = email.MessageId == BusyMessageId ? EmailInboundStatus.Busy : EmailInboundStatus.Accepted,
                    };
                });

            Reader = new ImapEmailMailboxReader(
                receiver.Object,
                Mock.Of<ICatalog<EmailMailboxSyncState>>(),
                Mock.Of<IEmailSecretProtector>(),
                Mock.Of<IClock>(),
                NullLogger<ImapEmailMailboxReader>.Instance);
        }

        public Mock<IMailFolder> Folder { get; } = new();

        public Dictionary<uint, uint> Sizes { get; } = new();

        public List<string> Received { get; } = [];

        public string FailingMessageId { get; set; }

        public string BusyMessageId { get; set; }

        public EmailMailboxSyncState State { get; } = new() { ItemId = "address-support", UidValidity = 1 };

        public EmailMailboxReadResult Result { get; } = new();

        public ImapEmailMailboxReader Reader { get; }

        public Task ReceiveAsync(params uint[] ids)
            => Reader.ReceiveBatchAsync(
                Folder.Object,
                ids.Select(id => new MailKit.UniqueId(1, id)).ToList(),
                destination: null,
                State,
                _address,
                Result,
                TestContext.Current.CancellationToken);

        private IMessageSummary Summary(MailKit.UniqueId uid)
        {
            var summary = new Mock<IMessageSummary>();
            summary.SetupGet(value => value.UniqueId).Returns(uid);
            summary.SetupGet(value => value.Size).Returns(Sizes.TryGetValue(uid.Id, out var size) ? size : 2_000u);

            return summary.Object;
        }

        private static MimeMessage Message(uint id)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Ann Lee", "ann@example.com"));
            message.To.Add(new MailboxAddress("Contoso Support", "support@contoso.com"));
            message.Subject = $"Order {id}";
            message.MessageId = $"m{id}@example.com";
            message.Body = new TextPart("plain") { Text = "Where is my order?" };

            return message;
        }
    }
}
