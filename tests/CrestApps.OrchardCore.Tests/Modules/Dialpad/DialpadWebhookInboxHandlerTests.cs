using System.Text.Json;
using CrestApps.OrchardCore.Dialpad.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Dialpad;

public sealed class DialpadWebhookInboxHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithPersistedCallEvent_InvokesDialpadWebhookService()
    {
        // Arrange
        var webhookService = new Mock<IDialpadWebhookService>();
        var handler = new DialpadWebhookInboxHandler(webhookService.Object);
        var callEvent = new DialpadCallEvent
        {
            CallId = "call-1",
            State = "ringing",
            EventTimestamp = 1_784_034_000_000,
        };
        var payload = JsonSerializer.Serialize(callEvent, DialpadJsonSerializerOptions.Default);

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        webhookService.Verify(
            service => service.ProcessAsync(
                It.Is<DialpadCallEvent>(value =>
                    value.CallId == callEvent.CallId &&
                    value.State == callEvent.State &&
                    value.EventTimestamp == callEvent.EventTimestamp),
                TestContext.Current.CancellationToken),
            Times.Once);
    }
}
