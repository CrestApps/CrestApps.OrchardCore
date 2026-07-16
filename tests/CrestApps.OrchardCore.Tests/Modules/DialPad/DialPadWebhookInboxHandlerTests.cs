using System.Text.Json;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookInboxHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithPersistedCallEvent_InvokesDialPadWebhookService()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var handler = new DialPadWebhookInboxHandler(webhookService.Object);
        var callEvent = new DialPadCallEvent
        {
            CallId = "call-1",
            State = "ringing",
            EventTimestamp = 1_784_034_000_000,
        };
        var payload = JsonSerializer.Serialize(callEvent, DialPadJsonSerializerOptions.Default);

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        webhookService.Verify(
            service => service.ProcessAsync(
                It.Is<DialPadCallEvent>(value =>
                    value.CallId == callEvent.CallId &&
                    value.State == callEvent.State &&
                    value.EventTimestamp == callEvent.EventTimestamp),
                TestContext.Current.CancellationToken),
            Times.Once);
    }
}
