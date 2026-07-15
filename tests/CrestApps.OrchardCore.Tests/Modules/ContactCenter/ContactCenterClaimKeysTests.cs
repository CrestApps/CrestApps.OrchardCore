using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterClaimKeysTests
{
    [Fact]
    public void BuildProviderEventIdempotencyKey_WithMaximumDeliveryId_StaysWithinIndexLimit()
    {
        // Arrange
        var providerName = new string('p', 100);
        var deliveryId = new string('d', 256);

        // Act
        var providerEventKey = ContactCenterClaimKeys.BuildProviderEventIdempotencyKey(providerName, deliveryId);
        var domainEventKey = ContactCenterClaimKeys.BuildProviderDomainEventIdempotencyKey(
            providerEventKey,
            new string('e', 128));

        // Assert
        Assert.True(providerEventKey.Length <= 128);
        Assert.True(domainEventKey.Length <= 128);
        Assert.NotEqual(providerEventKey, domainEventKey);
    }
}
