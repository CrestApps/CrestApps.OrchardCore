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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailConnectionTesterTests
{
    private const string Banner = "Internal billing service v2.4 ready";

    [Fact]
    public async Task TestSmtpAsync_WhenTheHostIsNotAMailServer_NeverShowsWhatTheHostAnswered()
    {
        // Arrange
        await using var server = await FakeServer.StartAsync(Banner);
        var tester = CreateTester();

        // Act
        var error = await tester.TestSmtpAsync(Settings(server.Port), TestContext.Current.CancellationToken);

        // Assert: the test cannot be used to learn which hosts and ports answer on the site's network.
        Assert.Equal(EmailServerConnection.UnreachableMessage, error);
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
        Assert.Equal(EmailServerConnection.UnreachableMessage, error);
    }

    [Fact]
    public void DescribeFailure_TellsARefusedSignInApartFromEveryOtherFailure()
    {
        // Act & Assert
        Assert.Equal(EmailServerConnection.RefusedSignInMessage, EmailServerConnection.DescribeFailure(new AuthenticationException("535 5.7.8 Bad credentials")));
        Assert.Equal(EmailServerConnection.UnreachableMessage, EmailServerConnection.DescribeFailure(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Equal(EmailServerConnection.UnreachableMessage, EmailServerConnection.DescribeFailure(new IOException("The handshake failed.")));
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
        var description = EmailServerConnection.DescribeFailure(failure);

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

        // Assert: the password goes only to the account it was saved for; any other server is not even contacted.
        if (expected)
        {
            await connecting;
            client.Verify(value => value.AuthenticateAsync(userName.Trim(), "s3cret", It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            await Assert.ThrowsAsync<AuthenticationException>(() => connecting);
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
            NullLogger<ImapEmailMailboxReader>.Instance);

        return new EmailConnectionTester(Mock.Of<IEmailSecretProtector>(), reader, NullLogger<EmailConnectionTester>.Instance);
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
