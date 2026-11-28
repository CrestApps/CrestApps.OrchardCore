using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailAdminControllerTests
{
    private const string AddressId = "address-support";

    [Theory]
    [InlineData("evil.example", 587, EmailConnectionSecurity.StartTls, "support@contoso.com")]
    [InlineData("smtp.contoso.com", 2525, EmailConnectionSecurity.StartTls, "support@contoso.com")]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.None, "support@contoso.com")]
    [InlineData("smtp.contoso.com", 587, EmailConnectionSecurity.StartTls, "sales@contoso.com")]
    public async Task TestConnection_WhenTheSettingsAreNotTheStoredOnes_NeverSendsTheStoredPassword(string host, int port, EmailConnectionSecurity security, string userName)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var outcome = await harness.TestAsync("smtp", host, port, security, userName, password: string.Empty);

        // Assert: the person is asked to type the password; nothing connects anywhere with the stored one.
        Assert.False(outcome.GetProperty("succeeded").GetBoolean());
        harness.Tester.Verify(tester => tester.TestSmtpAsync(It.IsAny<EmailServerSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestConnection_WhenTheSettingsAreTheStoredOnes_TestsWithTheStoredPassword()
    {
        // Arrange
        var harness = new Harness();

        // Act: the port left at 0 is the default 587 the password was saved with.
        var outcome = await harness.TestAsync("smtp", "SMTP.contoso.com", 0, EmailConnectionSecurity.StartTls, "support@contoso.com", password: string.Empty);

        // Assert
        Assert.True(outcome.GetProperty("succeeded").GetBoolean());
        harness.Tester.Verify(tester => tester.TestSmtpAsync(It.Is<EmailServerSettings>(server => server.Password == "stored-smtp"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnection_WithATypedPassword_TestsAnyServerWithIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var outcome = await harness.TestAsync("imap", "imap.example.net", 993, EmailConnectionSecurity.SslOnConnect, "ann@example.net", password: "typed");

        // Assert
        Assert.True(outcome.GetProperty("succeeded").GetBoolean());
        harness.Tester.Verify(tester => tester.TestImapAsync(It.Is<EmailMailboxSettings>(mailbox => mailbox.Server.Password.StartsWith("protected:", StringComparison.Ordinal) && mailbox.Server.Password.EndsWith("\0typed", StringComparison.Ordinal)), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Harness
    {
        private readonly AdminController _controller;

        public Harness()
        {
            var address = new OmnichannelChannelEndpoint { ItemId = AddressId, Value = "support@contoso.com", Capabilities = [OmnichannelConstants.Channels.Email] };
            address.Put(new EmailAddressSettings
            {
                TransportName = EmailChannelConstants.Transports.Smtp,
                Smtp = new EmailServerSettings
                {
                    Host = "smtp.contoso.com",
                    Port = 587,
                    Security = EmailConnectionSecurity.StartTls,
                    UserName = "support@contoso.com",
                    Password = "stored-smtp",
                },
            });

            var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
            endpointManager
                .Setup(manager => manager.FindByIdAsync(AddressId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(address);

            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            var protector = new Mock<IEmailSecretProtector>();
            protector.Setup(value => value.Protect(It.IsAny<string>())).Returns((string secret) => $"protected:{secret}");

            Tester
                .Setup(tester => tester.TestSmtpAsync(It.IsAny<EmailServerSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);
            Tester
                .Setup(tester => tester.TestImapAsync(It.IsAny<EmailMailboxSettings>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string)null);

            _controller = new AdminController(
                authorization.Object,
                endpointManager.Object,
                Tester.Object,
                protector.Object,
                new PassThroughStringLocalizer<AdminController>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) },
                },
            };
        }

        public Mock<IEmailConnectionTester> Tester { get; } = new();

        public async Task<JsonElement> TestAsync(string kind, string host, int port, EmailConnectionSecurity security, string userName, string password)
        {
            var result = await _controller.TestConnection(kind, AddressId, host, port, security, userName, password, folder: "INBOX");

            return JsonSerializer.SerializeToElement(Assert.IsType<JsonResult>(result).Value);
        }
    }
}
