using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class ActivityPurgeHelperTests
{
    [Fact]
    public void Purge_SetsPurgedStatusAndClearsReservation()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            Status = ActivityStatus.Reserved,
            ReservationId = "reservation-1",
            ReservedById = "user-1",
            ReservedByUsername = "agent",
            ReservedUtc = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc),
            ReservationExpiresUtc = new DateTime(2026, 7, 11, 12, 5, 0, DateTimeKind.Utc),
        };

        // Act
        ActivityPurgeHelper.Purge(activity);

        // Assert
        Assert.Equal(ActivityStatus.Purged, activity.Status);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
    }
}
