using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Controllers;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookControllerTests
{
    private const string Payload = "{\"call_id\":\"c1\",\"state\":\"ringing\"}";

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

    [Fact]
    public async Task Call_WhenPayloadExceedsLimit_ReturnsPayloadTooLarge()
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
        controller.Request.ContentLength = DialPadWebhookController.MaximumRequestBodySizeBytes + 1;

        // Act
        var result = await controller.Call();

        // Assert
        var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCodeResult.StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenServerRejectsChunkedPayload_ReturnsPayloadTooLarge()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var controller = CreateController(
            webhookService,
            new DialPadSettings
            {
                IsEnabled = true,
            },
            new EphemeralDataProtectionProvider());
        controller.Request.Body = new PayloadTooLargeStream();

        // Act
        var result = await controller.Call();

        // Assert
        var statusCodeResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCodeResult.StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenPayloadIsValid_DoesNotPassRequestCancellationToProcessing()
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var webhookService = new Mock<IDialPadWebhookService>();
        webhookService
            .Setup(service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialPadWebhookResult.Updated);
        var controller = CreateController(
            webhookService,
            new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = protectedSecret,
            },
            dataProtectionProvider);
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(Payload, secret)));
        controller.HttpContext.RequestAborted = new CancellationTokenSource().Token;

        // Act
        var result = await controller.Call();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        webhookService.Verify(
            service => service.ProcessAsync(
                It.IsAny<DialPadCallEvent>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
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

    private static string CreateJwt(string payloadJson, string secret)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
