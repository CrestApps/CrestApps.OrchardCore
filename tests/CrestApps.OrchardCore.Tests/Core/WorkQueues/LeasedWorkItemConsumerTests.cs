using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.Tests.Core.WorkQueues;

/// <summary>
/// The records that moved onto the leased work queue keep their stored shape: their own status enums map onto the
/// queue's by value, and the queue's view of the status is not written into the document.
/// </summary>
public sealed class LeasedWorkItemConsumerTests
{
    [Fact]
    public void OutboxStatus_MatchesTheQueueStatusByValue()
        => AssertSameValues<OutboxMessageStatus>();

    [Fact]
    public void InboxStatus_MatchesTheQueueStatusByValue()
        => AssertSameValues<ProviderWebhookInboxStatus>();

    [Fact]
    public void OutboxMessage_MapsTheQueueStatusOntoItsOwn()
    {
        // Arrange
        var message = new ContactCenterOutboxMessage { Status = OutboxMessageStatus.Pending };
        ILeasedWorkItem item = message;

        // Act
        item.Status = LeasedWorkItemStatus.DeadLettered;

        // Assert
        Assert.Equal(OutboxMessageStatus.DeadLettered, message.Status);
        Assert.Equal(LeasedWorkItemStatus.DeadLettered, item.Status);
    }

    [Fact]
    public void InboxMessage_MapsTheQueueStatusOntoItsOwn()
    {
        // Arrange
        var message = new ProviderWebhookInboxMessage { Status = ProviderWebhookInboxStatus.Pending };
        ILeasedWorkItem item = message;

        // Act
        item.Status = LeasedWorkItemStatus.Completed;

        // Assert
        Assert.Equal(ProviderWebhookInboxStatus.Completed, message.Status);
    }

    [Fact]
    public void StoredDocuments_KeepOneStatusProperty()
    {
        // Act
        var outbox = JsonSerializer.SerializeToElement(new ContactCenterOutboxMessage { Status = OutboxMessageStatus.Claimed }, JOptions.Default);
        var inbox = JsonSerializer.SerializeToElement(new ProviderWebhookInboxMessage { Status = ProviderWebhookInboxStatus.Claimed }, JOptions.Default);

        // Assert
        Assert.Single(outbox.EnumerateObject(), property => property.Name.Equals("Status", StringComparison.OrdinalIgnoreCase));
        Assert.Single(inbox.EnumerateObject(), property => property.Name.Equals("Status", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSameValues<TStatus>()
        where TStatus : struct, Enum
    {
        var own = Enum.GetValues<TStatus>().ToDictionary(value => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture), value => value.ToString());
        var queue = Enum.GetValues<LeasedWorkItemStatus>().ToDictionary(value => (int)value, value => value.ToString());

        Assert.Equal(queue, own);
    }
}
