using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

/// <summary>
/// The activities list shows a retry's attempt number, but not how the attempt before it ended, so an agent picking up
/// attempt 2 could not tell a no-answer from a callback request without opening the earlier activity. Loading the earlier
/// activity for every row would add a query per page, so the retry carries the previous disposition from the moment it
/// is created.
/// </summary>
public sealed class OmnichannelActivityFollowUpsTests
{
    [Fact]
    public void TheNextAttempt_CarriesTheDispositionThePreviousAttemptEndedWith()
    {
        // Arrange
        var completed = new OmnichannelActivity
        {
            ItemId = "activity-1",
            Attempts = 1,
            DispositionId = "no-answer",
            Status = ActivityStatus.Completed,
        };

        // Act
        var next = OmnichannelActivityFollowUps.CreateNextAttempt(completed, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(2, next.Attempts);
        Assert.Equal("no-answer", next.PreviousDispositionId);
        Assert.Null(next.DispositionId);
    }
}
