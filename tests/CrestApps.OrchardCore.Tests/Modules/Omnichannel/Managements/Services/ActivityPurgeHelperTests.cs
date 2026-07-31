using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class ActivityPurgeHelperTests
{
    [Fact]
    public void Purge_SetsAuditMetadataClearsReservationAndPreservesAssignment()
    {
        // Arrange
        var purgedAtUtc = new DateTime(2026, 7, 11, 16, 0, 0, DateTimeKind.Utc);
        var activity = new OmnichannelActivity
        {
            Status = ActivityStatus.Reserved,
            AssignedToId = "assigned-user-1",
            AssignedToUsername = "assigned-agent",
            AssignedToUtc = new DateTime(2026, 7, 11, 11, 0, 0, DateTimeKind.Utc),
            AssignmentStatus = ActivityAssignmentStatus.Assigned,
            ReservationId = "reservation-1",
            ReservedById = "reserved-user-1",
            ReservedByUsername = "reserved-agent",
            ReservedUtc = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc),
            ReservationExpiresUtc = new DateTime(2026, 7, 11, 12, 5, 0, DateTimeKind.Utc),
        };

        // Act
        ActivityPurgeHelper.Purge(activity, purgedAtUtc, "purging-user-1", "purging-agent");

        // Assert
        Assert.Equal(ActivityStatus.Purged, activity.Status);
        Assert.Equal(purgedAtUtc, activity.PurgedAtUtc);
        Assert.Equal("purging-user-1", activity.PurgedById);
        Assert.Equal("purging-agent", activity.PurgedByUsername);
        Assert.Null(activity.ReservationId);
        Assert.Null(activity.ReservedById);
        Assert.Null(activity.ReservedByUsername);
        Assert.Null(activity.ReservedUtc);
        Assert.Null(activity.ReservationExpiresUtc);
        Assert.Equal("assigned-user-1", activity.AssignedToId);
        Assert.Equal("assigned-agent", activity.AssignedToUsername);
        Assert.Equal(new DateTime(2026, 7, 11, 11, 0, 0, DateTimeKind.Utc), activity.AssignedToUtc);
        Assert.Equal(ActivityAssignmentStatus.Assigned, activity.AssignmentStatus);
    }
}
