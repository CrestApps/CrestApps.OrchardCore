using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public class ContactCenterHandoffQueueOptionsProviderTests
{
    [Fact]
    public async Task GetQueuesAsync_ReturnsEnabledQueues_OrderedByName()
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ActivityQueue { ItemId = "q2", Name = "Support", Enabled = true },
                new ActivityQueue { ItemId = "q1", Name = "Billing", Enabled = true },
            ]);

        var provider = new ContactCenterHandoffQueueOptionsProvider(queueManager.Object);

        var options = await provider.GetQueuesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, options.Count);
        // Ordered by name.
        Assert.Equal("Billing", options[0].Name);
        Assert.Equal("q1", options[0].Id);
        Assert.Equal("Support", options[1].Name);
    }

    [Fact]
    public async Task GetQueuesAsync_WhenNoQueues_ReturnsEmpty()
    {
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var provider = new ContactCenterHandoffQueueOptionsProvider(queueManager.Object);

        var options = await provider.GetQueuesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(options);
    }
}
