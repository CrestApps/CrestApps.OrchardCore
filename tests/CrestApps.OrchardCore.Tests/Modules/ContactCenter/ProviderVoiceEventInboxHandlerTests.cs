using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceEventInboxHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithPersistedEvent_InvokesProviderEventService()
    {
        // Arrange
        var eventService = new Mock<IProviderVoiceEventService>();
        var handler = new ProviderVoiceEventInboxHandler(eventService.Object);
        var providerEvent = new ProviderVoiceEvent
        {
            ProviderName = "provider",
            ProviderCallId = "call-1",
            IdempotencyKey = "delivery-1",
            OccurredUtc = new DateTime(2026, 7, 14, 13, 0, 0, DateTimeKind.Utc),
        };

        // Act
        await handler.HandleAsync(JsonSerializer.Serialize(providerEvent), TestContext.Current.CancellationToken);

        // Assert
        eventService.Verify(
            service => service.IngestAsync(
                It.Is<ProviderVoiceEvent>(value =>
                    value.ProviderName == providerEvent.ProviderName &&
                    value.ProviderCallId == providerEvent.ProviderCallId &&
                    value.IdempotencyKey == providerEvent.IdempotencyKey),
                TestContext.Current.CancellationToken),
            Times.Once);
    }
}
