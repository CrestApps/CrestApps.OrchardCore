using System.Net;
using System.Net.Sockets;
using System.Text;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;
using MailKit.Security;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailConnectionTesterTests
{
    private const string Banner = "Internal billing service v2.4 ready";

    private const string RefusedSignInMessage = "The mail server refused the sign-in. Check the user name and password (or app password).";

    private const string UnreachableMessage = "The mail server could not be reached, or it did not answer as a mail server. Check the host, port and security; the details are in the site's log.";

    private static readonly IStringLocalizer Localizer = new PassThroughStringLocalizer<EmailConnectionTester>();

    [Fact]
    public async Task TestSmtpAsync_WhenTheHostIsNotAMailServer_NeverShowsWhatTheHostAnswered()
    {
        // Arrange
        await using var server = await FakeServer.StartAsync(Banner);
        var tester = CreateTester();

        // Act
        var error = await tester.TestSmtpAsync(Settings(server.Port), TestContext.Current.CancellationToken);

        // Assert: the test cannot be used to learn which hosts and ports answer on the site's network.
        Assert.Equal(UnreachableMessage, error);
        Assert.DoesNotContain("billing", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestImapAsync_WhenTheHostIsNotAMailServer_NeverShowsWhatTheHostAnswered()
    {
        // Arrange
        await using var server = await FakeServer.StartAsync(Banner);
        var tester = CreateTester();

        // Act
        var error = await tester.TestImapAsync(new EmailMailboxSettings { Server = Settings(server.Port) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(UnreachableMessage, error);
    }

    [Fact]
    public void DescribeFailure_TellsARefusedSignInApartFromEveryOtherFailure()
    {
        // Act & Assert
        Assert.Equal(RefusedSignInMessage, EmailServerConnection.DescribeFailure(new AuthenticationException("535 5.7.8 Bad credentials"), Localizer));
        Assert.Equal(UnreachableMessage, EmailServerConnection.DescribeFailure(new SocketException((int)SocketError.ConnectionRefused), Localizer));
        Assert.Equal(UnreachableMessage, EmailServerConnection.DescribeFailure(new IOException("The handshake failed."), Localizer));
    }

    [Fact]
    public void DescribeFailure_GoesThroughTheCallersLocalizer()
    {
        // Arrange
        var localizer = new Mock<IStringLocalizer>();
        localizer
            .Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string name, object[] arguments) => new LocalizedString(name, "fr: " + string.Format(name, arguments)));
        localizer
            .Setup(value => value[It.IsAny<string>()])
            .Returns((string name) => new LocalizedString(name, "fr: " + name));

        // Act & Assert: every failure, including the ones that name the host, is shown in the reader's culture.
        Assert.Equal("fr: " + RefusedSignInMessage, EmailServerConnection.DescribeFailure(new AuthenticationException("535"), localizer.Object));
        Assert.Equal("fr: " + UnreachableMessage, EmailServerConnection.DescribeFailure(new IOException("x"), localizer.Object));
        Assert.StartsWith("fr: The mail server at 'smtp.contoso.com' did not offer", EmailServerConnection.DescribeFailure(new EmailConnectionNotSecureException("smtp.contoso.com"), localizer.Object), StringComparison.Ordinal);
        Assert.StartsWith("fr: The stored password for the mail server at 'smtp.contoso.com'", EmailServerConnection.DescribeFailure(new EmailStoredPasswordUnusableException("smtp.contoso.com"), localizer.Object), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConnectAsync_APasswordSavedForTheImapServer_IsNeverSentToTheSmtpServer()
    {
        // Arrange: the same host, port, security and user name in both boxes, and the IMAP password copied into the SMTP
        // box (through the API, a recipe or an import).
        var protector = new ReversibleProtector();
        var server = new EmailServerSettings { Host = "mail.contoso.com", Port = 993, Security = EmailConnectionSecurity.SslOnConnect, UserName = "support@contoso.com" };
        server.Password = EmailServerConnection.ProtectPassword(protector, server, EmailServerConnection.ImapPort, EmailServerConnection.ImapImplicitTlsPort, "s3cret");

        var smtp = new Mock<MailKit.IMailService>();
        smtp.SetupGet(value => value.IsSecure).Returns(true);

        var imap = new Mock<MailKit.Net.Imap.IImapClient>();
        imap.SetupGet(value => value.IsSecure).Returns(true);

        // Act & Assert: the IMAP client reads it back; any other client is refused before connecting.
        await Assert.ThrowsAsync<EmailStoredPasswordUnusableException>(() => EmailServerConnection.ConnectAsync(smtp.Object, server, 993, protector, TestContext.Current.CancellationToken));
        smtp.Verify(value => value.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>()), Times.Never);

        await EmailServerConnection.ConnectAsync(imap.Object, server, 993, protector, TestContext.Current.CancellationToken);
        imap.Verify(value => value.AuthenticateAsync("support@contoso.com", "s3cret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("SMTP.Contoso.com ", 587, EmailConnectionSecurity.StartTls, "Support@Contoso.com", true)]
    [InlineData("smtp.contoso.com", 0, EmailConnectionSecurity.StartTls, "support@contoso.com", true)]
    [InlineData("evil.example", 587, EmailConnectionSecurity.StartTls, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 2525, EmailConnectionSecurity.StartTls, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.None, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.StartTls, "sales@contoso.com", false)]
    [InlineData("", 587, EmailConnectionSecurity.StartTls, "support@contoso.com", false)]
    public void IsSameAccount_IsOnlyTheHostPortSecurityAndUserNameThePasswordWasSavedWith(string host, int port, EmailConnectionSecurity security, string userName, bool expected)
    {
        // Arrange
        var stored = new EmailServerSettings
        {
            Host = "smtp.contoso.com",
            Port = 587,
            Security = EmailConnectionSecurity.StartTls,
            UserName = "support@contoso.com",
            Password = "protected",
        };

        var typed = new EmailServerSettings { Host = host, Port = port, Security = security, UserName = userName };

        // Act
        var isSame = EmailServerConnection.IsSameAccount(stored, typed, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort);

        // Assert
        Assert.Equal(expected, isSame);
    }

    [Fact]
    public void DescribeFailure_ShowsWhyAPasswordWasNotSentOverAnUnencryptedConnection()
    {
        // Arrange
        var failure = new EmailConnectionNotSecureException("smtp.contoso.com");

        // Act
        var description = EmailServerConnection.DescribeFailure(failure, Localizer);

        // Assert
        Assert.Equal(failure.Message, description);
        Assert.Contains("STARTTLS", description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.StartTls, "support@contoso.com", true)]
    [InlineData("SMTP.Contoso.com ", 0, EmailConnectionSecurity.StartTls, "Support@Contoso.com", true)]
    [InlineData("evil.example", 587, EmailConnectionSecurity.StartTls, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 2525, EmailConnectionSecurity.StartTls, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.None, "support@contoso.com", false)]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.StartTls, "sales@contoso.com", false)]
    public async Task ConnectAsync_ReadsAProtectedPasswordOnlyForTheAccountItWasSavedFor(string host, int port, EmailConnectionSecurity security, string userName, bool expected)
    {
        // Arrange: the password protected for one account, then copied next to other settings, as the channel
        // endpoints API, a recipe or an import can do without the editor's checks.
        var protector = new ReversibleProtector();
        var saved = new EmailServerSettings { Host = "smtp.contoso.com", Port = 587, Security = EmailConnectionSecurity.StartTls, UserName = "support@contoso.com" };
        var moved = new EmailServerSettings
        {
            Host = host,
            Port = port,
            Security = security,
            UserName = userName,
            Password = EmailServerConnection.ProtectPassword(protector, saved, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort, "s3cret"),
        };

        var client = new Mock<MailKit.IMailService>();
        client.SetupGet(value => value.IsSecure).Returns(true);

        // Act
        var connecting = EmailServerConnection.ConnectAsync(
            client.Object,
            moved,
            EmailServerConnection.ResolvePort(moved, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort),
            protector,
            TestContext.Current.CancellationToken);

        // Assert: the password goes only to the account it was saved for; any other server is not even contacted, and
        // the admin is told to type the password again rather than that the server refused it.
        if (expected)
        {
            await connecting;
            client.Verify(value => value.AuthenticateAsync(userName.Trim(), "s3cret", It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            var failure = await Assert.ThrowsAsync<EmailStoredPasswordUnusableException>(() => connecting);
            Assert.Equal(failure.Message, EmailServerConnection.DescribeFailure(failure, Localizer));
            client.Verify(value => value.ConnectAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<SecureSocketOptions>(), It.IsAny<CancellationToken>()), Times.Never);
            client.Verify(value => value.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Fact]
    public async Task ConnectAsync_WhenTheServerOffersNoEncryption_ThrowsWithoutSendingThePassword()
    {
        // Arrange
        var protector = new ReversibleProtector();
        var server = new EmailServerSettings { Host = "relay.contoso.com", Port = 25, Security = EmailConnectionSecurity.Auto, UserName = "support@contoso.com" };
        server.Password = EmailServerConnection.ProtectPassword(protector, server, EmailServerConnection.SmtpSubmissionPort, EmailServerConnection.SmtpImplicitTlsPort, "s3cret");

        var client = new Mock<MailKit.IMailService>();
        client.SetupGet(value => value.IsSecure).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<EmailConnectionNotSecureException>(() => EmailServerConnection.ConnectAsync(client.Object, server, 25, protector, TestContext.Current.CancellationToken));
        client.Verify(value => value.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static EmailServerSettings Settings(int port)
        => new()
        {
            Host = IPAddress.Loopback.ToString(),
            Port = port,
            Security = EmailConnectionSecurity.None,
        };

    private static EmailConnectionTester CreateTester()
    {
        var reader = new ImapEmailMailboxReader(
            Mock.Of<IEmailInboundReceiver>(),
            Mock.Of<ICatalog<EmailMailboxSyncState>>(),
            Mock.Of<IEmailSecretProtector>(),
            Mock.Of<IClock>(),
            NullLogger<ImapEmailMailboxReader>.Instance,
            new PassThroughStringLocalizer<ImapEmailMailboxReader>());

        return new EmailConnectionTester(Mock.Of<IEmailSecretProtector>(), reader, NullLogger<EmailConnectionTester>.Instance, new PassThroughStringLocalizer<EmailConnectionTester>());
    }

    private sealed class ReversibleProtector : IEmailSecretProtector
    {
        public string Protect(string secret)
            => secret is null ? null : "protected:" + secret;

        public string Unprotect(string protectedSecret)
            => protectedSecret is not null && protectedSecret.StartsWith("protected:", StringComparison.Ordinal)
                ? protectedSecret["protected:".Length..]
                : null;
    }

    // A server on the loopback address that greets whoever connects with a line that is not a mail server's, then
    // hangs up: what an internal service the test is pointed at would do.
    private sealed class FakeServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serving;

        private FakeServer(TcpListener listener, string banner)
        {
            _listener = listener;
            _serving = ServeAsync(listener, banner, TestContext.Current.CancellationToken);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public static Task<FakeServer> StartAsync(string banner)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            return Task.FromResult(new FakeServer(listener, banner));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();

            try
            {
                await _serving;
            }
            catch (Exception)
            {
                // The listener was stopped before anybody connected.
            }
        }

        private static async Task ServeAsync(TcpListener listener, string banner, CancellationToken cancellationToken)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            var stream = client.GetStream();

            await stream.WriteAsync(Encoding.ASCII.GetBytes(banner + "\r\n"), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }
}
