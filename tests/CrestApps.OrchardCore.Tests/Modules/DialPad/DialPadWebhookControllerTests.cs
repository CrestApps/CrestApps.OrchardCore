using CrestApps.OrchardCore.DialPad.Controllers;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookControllerTests
{
    [Fact]
    public async Task Call_WhenSigningSecretIsMissing_RejectsWebhook()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var controller = CreateController(
            webhookService,
            new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = null,
            },
            new EphemeralDataProtectionProvider());

        // Act
        var result = await controller.Call();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenSigningSecretCannotBeUnprotected_FailsClosed()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var controller = CreateController(
            webhookService,
            new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = "not-a-protected-secret",
            },
            new EphemeralDataProtectionProvider());

        // Act
        var result = await controller.Call();

        // Assert
        var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCodeResult.StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DialPadWebhookController CreateController(
        Mock<IDialPadWebhookService> webhookService,
        DialPadSettings settings,
        IDataProtectionProvider dataProtectionProvider)
    {
        var controller = new DialPadWebhookController(
            webhookService.Object,
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            NullLogger<DialPadWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.Request.Body = new MemoryStream("signed-payload"u8.ToArray());

        return controller;
    }
}
