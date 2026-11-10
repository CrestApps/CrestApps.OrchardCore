using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// AI events moved to the one Webhooks building block. The subscriptions and pending deliveries the AI webhooks held
/// must move with them, signed with the same secret, and must never be sent twice.
/// </summary>
public sealed class AIWebhookImporterTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Import_MovesSubscriptionsWithTheirSecret_AndPendingDeliveries_ThenSettlesTheOldOnes()
    {
        // Arrange
        var protection = new EphemeralDataProtectionProvider();
        var aiSubscription = new AIWebhookSubscription
        {
            ItemId = "ai-sub-1",
            Name = "CRM",
            Url = "https://crm.example.com/hook",
            Events = [AIWebhookEvents.ConversationEnded],
            ProtectedSecret = protection.CreateProtector(AIWebhookPublisher.ProtectorPurpose).Protect("ai-secret"),
            Enabled = true,
            CreatedUtc = _now.AddDays(-10),
        };
        var aiDelivery = new AIWebhookDelivery
        {
            ItemId = "ai-del-1",
            SubscriptionId = "ai-sub-1",
            EventName = AIWebhookEvents.ConversationEnded,
            Payload = "{\"id\":\"ai-del-1\"}",
            Status = AIWebhookDeliveryStatus.Pending,
            Attempts = 2,
            NextAttemptUtc = _now.AddMinutes(5),
            CreatedUtc = _now.AddHours(-1),
        };

        var aiSubscriptions = new Mock<IAIWebhookSubscriptionStore>();
        aiSubscriptions.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([aiSubscription]);
        var aiDeliveries = new Mock<IAIWebhookDeliveryStore>();
        aiDeliveries.Setup(store => store.ListDueAsync(DateTime.MaxValue, int.MaxValue, It.IsAny<CancellationToken>())).ReturnsAsync([aiDelivery]);

        var createdSubscriptions = new List<WebhookSubscription>();
        var subscriptions = new Mock<IWebhookSubscriptionStore>();
        subscriptions.Setup(store => store.CreateAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookSubscription, CancellationToken>((subscription, _) => createdSubscriptions.Add(subscription))
            .Returns(ValueTask.CompletedTask);
        var createdDeliveries = new List<WebhookDelivery>();
        var deliveries = new Mock<IWebhookDeliveryStore>();
        deliveries.Setup(store => store.CreateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((delivery, _) => createdDeliveries.Add(delivery))
            .Returns(ValueTask.CompletedTask);

        var dispatcher = new WebhookDispatcher(Mock.Of<CrestApps.OrchardCore.Webhooks.IWebhookSender>(), deliveries.Object, subscriptions.Object, [], protection, Clock(), NullLogger<WebhookDispatcher>.Instance);
        var importer = new AIWebhookImporter(aiSubscriptions.Object, aiDeliveries.Object, subscriptions.Object, deliveries.Object, dispatcher, protection, Clock(), NullLogger<AIWebhookImporter>.Instance, Mock.Of<global::YesSql.ISession>());

        // Act
        var (movedSubscriptions, movedDeliveries) = await importer.ImportAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, movedSubscriptions);
        Assert.Equal(1, movedDeliveries);

        var subscription = Assert.Single(createdSubscriptions);
        Assert.Equal("ai-sub-1", subscription.ItemId);
        Assert.Equal("https://crm.example.com/hook", subscription.Url);
        Assert.Equal([AIWebhookEvents.ConversationEnded], subscription.Events);
        Assert.True(subscription.IsEnabled);
        Assert.Equal("ai-secret", dispatcher.GetSecret(subscription));

        var delivery = Assert.Single(createdDeliveries);
        Assert.Equal("ai-sub-1", delivery.SubscriptionId);
        Assert.Equal("ai-del-1", delivery.EventId);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(2, delivery.Attempts);
        Assert.Equal(_now.AddMinutes(5), delivery.NextAttemptUtc);

        // The old records are settled, not deleted, so nothing is sent twice and the old log stays.
        Assert.False(aiSubscription.Enabled);
        Assert.Equal(AIWebhookDeliveryStatus.Failed, aiDelivery.Status);
        Assert.Equal(AIWebhookImporter.MovedError, aiDelivery.Error);
        aiSubscriptions.Verify(store => store.UpdateAsync(aiSubscription, It.IsAny<CancellationToken>()), Times.Once);
        aiDeliveries.Verify(store => store.UpdateAsync(aiDelivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Import_SkipsASubscriptionAlreadyMoved()
    {
        // Arrange
        var protection = new EphemeralDataProtectionProvider();
        var aiSubscriptions = new Mock<IAIWebhookSubscriptionStore>();
        aiSubscriptions.Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new AIWebhookSubscription { ItemId = "ai-sub-1" }]);
        var aiDeliveries = new Mock<IAIWebhookDeliveryStore>();
        aiDeliveries.Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var subscriptions = new Mock<IWebhookSubscriptionStore>();
        subscriptions.Setup(store => store.FindByIdAsync("ai-sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(new WebhookSubscription { ItemId = "ai-sub-1" });
        var deliveries = new Mock<IWebhookDeliveryStore>();
        var dispatcher = new WebhookDispatcher(Mock.Of<CrestApps.OrchardCore.Webhooks.IWebhookSender>(), deliveries.Object, subscriptions.Object, [], protection, Clock(), NullLogger<WebhookDispatcher>.Instance);
        var importer = new AIWebhookImporter(aiSubscriptions.Object, aiDeliveries.Object, subscriptions.Object, deliveries.Object, dispatcher, protection, Clock(), NullLogger<AIWebhookImporter>.Instance, Mock.Of<global::YesSql.ISession>());

        // Act
        var (movedSubscriptions, _) = await importer.ImportAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, movedSubscriptions);
        subscriptions.Verify(store => store.CreateAsync(It.IsAny<WebhookSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("Webhooks", WebhookStorage.CollectionName);
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);
}
