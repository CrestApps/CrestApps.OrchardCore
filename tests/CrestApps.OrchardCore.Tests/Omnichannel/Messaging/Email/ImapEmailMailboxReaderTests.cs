using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using MailKit;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using MailKit.Search;
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
    public async Task ReceiveBatchAsync_AnEmailTheReceiverFailsOn_IsKeptForTheNextReadWithoutHoldingUpTheMailbox()
    {
        // Arrange: the receiver fails on one email, as it does while the database is briefly out of reach.
        var harness = new Harness();
        harness.FailingMessageId = "m2@example.com";

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert: the read moves on to the mail after it, and the email is kept for the next read to try again.
        Assert.Equal(new[] { "m1@example.com", "m2@example.com", "m3@example.com" }, harness.Received);
        Assert.Equal(2, harness.Result.Received);
        Assert.Equal(3u, harness.State.LastUid);
        Assert.Equal(1, Assert.Contains(2u, harness.State.RetryUids));
    }

    [Fact]
    public async Task ReceiveBatchAsync_AKeptEmailTheReceiverTakesLater_IsReceivedAndForgotten()
    {
        // Arrange
        var harness = new Harness();
        harness.State.LastUid = 3;
        harness.State.RetryUids[2] = 1;

        // Act
        await harness.ReceiveAsync(2);

        // Assert
        Assert.Equal(new[] { "m2@example.com" }, harness.Received);
        Assert.Equal(1, harness.Result.Received);
        Assert.Empty(harness.State.RetryUids);
        Assert.Equal(3u, harness.State.LastUid);
    }

    [Fact]
    public async Task ReceiveBatchAsync_AnEmailThatFailsOnEveryTry_IsPassedOverAfterTheLast()
    {
        // Arrange
        var harness = new Harness();
        harness.FailingMessageId = "m2@example.com";
        harness.State.LastUid = 3;
        harness.State.RetryUids[2] = ImapEmailMailboxReader.MaxReceiveAttempts - 1;

        // Act
        await harness.ReceiveAsync(2);

        // Assert: a failure that keeps coming back is the email's own, so it is not tried for ever.
        Assert.Empty(harness.State.RetryUids);
        Assert.Equal(0, harness.Result.Received);
    }

    [Fact]
    public async Task ReceiveBatchAsync_TrustsOnlyTheMailboxHostsOwnAuthenticationResults()
    {
        // Arrange: the host stamped email 1 with a DMARC pass for example.com, the From domain. Email 2 failed DMARC at
        // the host, and its sender wrote a passing header of its own further down, which proves nothing.
        var harness = new Harness();
        harness.AuthenticationResults[1] = ["mx.contoso.com; dmarc=pass (p=reject) header.from=example.com"];
        harness.AuthenticationResults[2] =
        [
            "mx.contoso.com; dmarc=fail (p=none) header.from=example.com",
            "forged.example; dmarc=pass header.from=example.com",
        ];

        // Act
        await harness.ReceiveAsync(1, 2, 3);

        // Assert
        Assert.Equal(new[] { true, false, false }, harness.Emails.Select(email => email.IsFromDomainAuthenticated));
    }

    [Fact]
    public async Task ReceiveBatchAsync_WithATrustedServer_BelievesOnlyThatServersAuthenticationResults()
    {
        // Arrange: the host is mx.google.com. Email 1 carries its pass. Email 2 reached a host that added no header, so
        // the sender's own forged pass is on top. Email 3 has the host's fail on top of a forged pass below it. Email 4
        // has a forged pass on top, above the host's own pass.
        var harness = new Harness { TrustedAuthenticationServerId = "MX.google.com" };
        harness.AuthenticationResults[1] = ["mx.google.com; dkim=pass header.d=example.com; dmarc=pass (p=REJECT) header.from=example.com"];
        harness.AuthenticationResults[2] = ["forged.example; dmarc=pass header.from=example.com"];
        harness.AuthenticationResults[3] =
        [
            "mx.google.com; dmarc=fail (p=NONE) header.from=example.com",
            "mx.google.com; dmarc=pass header.from=example.com",
        ];
        harness.AuthenticationResults[4] =
        [
            "forged.example; dmarc=pass header.from=example.com",
            "mx.google.com (the host); dmarc=pass header.from=example.com",
        ];

        // Act
        await harness.ReceiveAsync(1, 2, 3, 4);

        // Assert: only the trusted server's topmost report counts, wherever it is.
        Assert.Equal(new[] { true, false, false, true }, harness.Emails.Select(email => email.IsFromDomainAuthenticated));
    }

    [Theory]
    [InlineData("mx.google.com; dmarc=pass header.from=example.com", "mx.google.com")]
    [InlineData("  mx.google.com 1 ; dmarc=pass", "mx.google.com")]
    [InlineData("(comment) mx.contoso.com; spf=pass", "mx.contoso.com")]
    [InlineData("spf=pass (sender IP is 1.2.3.4) smtp.mailfrom=example.com; dmarc=pass header.from=example.com", null)]
    [InlineData("", null)]
    public void GetServerId_ReadsTheFirstWordOfTheHeader(string header, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, InboundEmailAuthentication.GetServerId(header));
    }

    [Fact]
    public async Task FindNewMailAsync_TriesAKeptEmailBeforeTheNewMail()
    {
        // Arrange: UIDs 1 to 5 are in the folder, 3 was the last read, and the receiver failed on 2.
        var harness = new Harness();
        harness.State.LastUid = 3;
        harness.State.RetryUids[2] = 1;
        harness.State.RetryUids[9] = 1;
        harness.FolderUids.AddRange([1u, 2u, 3u, 4u, 5u]);
        harness.Folder.SetupGet(folder => folder.UidValidity).Returns(1u);

        // Act
        var uids = await harness.Reader.FindNewMailAsync(harness.Folder.Object, harness.State, new EmailMailboxSettings(), TestContext.Current.CancellationToken);

        // Assert: 9 is gone from the folder (a person moved it), so it is forgotten.
        Assert.Equal(new[] { 2u, 4u, 5u }, uids.Select(uid => uid.Id));
        Assert.Equal(new[] { 2u }, harness.State.RetryUids.Keys);
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
                .ReturnsAsync((MailKit.UniqueId uid, CancellationToken _, ITransferProgress _) =>
                {
                    var message = Message(uid.Id);

                    foreach (var header in AuthenticationResults.TryGetValue(uid.Id, out var headers) ? headers : [])
                    {
                        message.Headers.Add("Authentication-Results", header);
                    }

                    return message;
                });

            // A UID search answers with the folder's UIDs it names: a range ending in * names every UID from its start.
            Folder
                .Setup(folder => folder.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SearchQuery query, CancellationToken _) =>
                {
                    var uids = ((UidSearchQuery)query).Uids;
                    var found = uids is UniqueIdRange range
                        ? FolderUids.Where(id => id >= range.Start.Id)
                        : FolderUids.Where(id => uids.Any(uid => uid.Id == id));

                    return found.Select(id => new MailKit.UniqueId(1, id)).ToList();
                });

            var receiver = new Mock<IEmailInboundReceiver>();
            receiver
                .Setup(value => value.ReceiveAsync(It.IsAny<InboundEmail>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((InboundEmail email, string _, bool _, CancellationToken _) =>
                {
                    Received.Add(email.MessageId);
                    Emails.Add(email);

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
                NullLogger<ImapEmailMailboxReader>.Instance,
                new PassThroughStringLocalizer<ImapEmailMailboxReader>());
        }

        public Mock<IMailFolder> Folder { get; } = new();

        public Dictionary<uint, uint> Sizes { get; } = new();

        public List<uint> FolderUids { get; } = [];

        public List<string> Received { get; } = [];

        public List<InboundEmail> Emails { get; } = [];

        // Authentication-Results headers per UID, topmost first, as the mailbox holds them.
        public Dictionary<uint, string[]> AuthenticationResults { get; } = new();

        public string FailingMessageId { get; set; }

        public string BusyMessageId { get; set; }

        public string TrustedAuthenticationServerId { get; set; }

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
                TrustedAuthenticationServerId,
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
