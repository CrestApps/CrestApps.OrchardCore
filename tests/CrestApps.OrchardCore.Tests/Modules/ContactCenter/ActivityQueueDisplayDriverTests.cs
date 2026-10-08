using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The queue editor was split into cards (General, Routing, Service levels, Skills, Hours and overflow, Limits, While
/// callers wait, Callback) that all post one form, confirmed live. These pin what the driver stores from that post.
/// </summary>
public sealed class ActivityQueueDisplayDriverTests
{
    [Theory]
    [InlineData("  media-1 ", "media-1")]
    [InlineData("media-1", "media-1")]
    [InlineData("   ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task UpdateAsync_StoresTheHoldMusicTrimmed_AndNoneWhenBlank(string posted, string expected)
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var model = new QueueViewModel
        {
            Name = "Support",
            Treatment = new QueueTreatmentViewModel { HoldMusicMediaId = posted },
        };

        // Act
        await CreateDriver().UpdateAsync(queue, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Equal(expected, queue.Treatment.HoldMusicMediaId);
    }

    // A queue that overflowed into itself would hand a waiting caller straight back to the queue they are waiting in.
    [Fact]
    public async Task UpdateAsync_DropsAnOverflowBackToTheSameQueue()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var model = new QueueViewModel
        {
            Name = "Support",
            OverflowQueueId = "queue-1",
            OverflowTargets =
            [
                new QueueOverflowTargetViewModel { QueueId = "queue-2", AfterSeconds = 120 },
                new QueueOverflowTargetViewModel { QueueId = " QUEUE-1 ", AfterSeconds = 30 },
                new QueueOverflowTargetViewModel { QueueId = "queue-3", AfterSeconds = 60 },
            ],
        };

        // Act
        await CreateDriver().UpdateAsync(queue, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Null(queue.OverflowQueueId);
        Assert.Equal(["queue-3", "queue-2"], queue.OverflowTargets.Select(target => target.QueueId));
    }

    [Fact]
    public async Task UpdateAsync_KeepsAnOverflowToAnotherQueue()
    {
        // Arrange
        var queue = new ActivityQueue { ItemId = "queue-1" };
        var model = new QueueViewModel { Name = "Support", OverflowQueueId = " queue-2 " };

        // Act
        await CreateDriver().UpdateAsync(queue, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Equal("queue-2", queue.OverflowQueueId);
    }

    private static ActivityQueueDisplayDriver CreateDriver()
        => new(AdminFormOptionsProviderFactory.Create(), Mock.Of<IActivityQueueGroupManager>(), []);
}
