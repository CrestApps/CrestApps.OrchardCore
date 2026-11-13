using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class EnrollmentRulesTests
{
    [Theory]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Withdrawn, true)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Suspended, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Expired, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Withdrawn, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Pending, false)]
    [InlineData(EnrollmentStatus.Suspended, EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Suspended, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Expired, EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.Expired, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Active, false)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Withdrawn, false)]
    [InlineData(EnrollmentStatus.Withdrawn, EnrollmentStatus.Active, false)]
    public void Transitions_AllowOnlyThePlannedMoves(EnrollmentStatus from, EnrollmentStatus to, bool allowed)
    {
        Assert.Equal(allowed, EnrollmentTransitions.Table.CanMove(from, to));
    }

    [Theory]
    [InlineData(EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Withdrawn, true)]
    [InlineData(EnrollmentStatus.Active, false)]
    [InlineData(EnrollmentStatus.Expired, false)]
    public void Transitions_CompletedAndWithdrawnAreFinal(EnrollmentStatus status, bool terminal)
    {
        Assert.Equal(terminal, EnrollmentTransitions.Table.IsTerminal(status));
    }

    [Fact]
    public void AccessEnds_IsNull_WhenAnyGrantHasNoEnd()
    {
        // Arrange
        var grants = new[]
        {
            new EnrollmentGrant { Source = "Order", SourceId = "1", AccessEndsUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new EnrollmentGrant { Source = "Manual", SourceId = "2", AccessEndsUtc = null },
        };

        // Act
        var ends = EnrollmentAccess.ComputeAccessEnds(grants);

        // Assert
        Assert.Null(ends);
    }

    [Fact]
    public void AccessEnds_IsTheLatestEnd_WhenEveryGrantEnds()
    {
        // Arrange
        var grants = new[]
        {
            new EnrollmentGrant { Source = "Order", SourceId = "1", AccessEndsUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new EnrollmentGrant { Source = "Licence", SourceId = "2", AccessEndsUtc = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
        };

        // Act
        var ends = EnrollmentAccess.ComputeAccessEnds(grants);

        // Assert
        Assert.Equal(new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc), ends);
    }

    [Fact]
    public void HasAccess_NeedsAnActiveEnrollmentWithAGrantThatHasNotEnded()
    {
        // Arrange
        var now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var enrollment = new Enrollment
        {
            Status = EnrollmentStatus.Active,
            Grants = [new EnrollmentGrant { Source = "Manual", SourceId = "1", AccessEndsUtc = now.AddDays(1) }],
            AccessEndsUtc = now.AddDays(1),
        };

        // Act & Assert
        Assert.True(EnrollmentAccess.HasAccess(enrollment, now));
        Assert.False(EnrollmentAccess.HasAccess(enrollment, now.AddDays(2)));

        enrollment.Status = EnrollmentStatus.Suspended;
        Assert.False(EnrollmentAccess.HasAccess(enrollment, now));

        enrollment.Status = EnrollmentStatus.Active;
        enrollment.Grants.Clear();
        Assert.False(EnrollmentAccess.HasAccess(enrollment, now));
    }

    [Fact]
    public void EnrollmentKeys_VerifyTheKeyAgainstItsHash_AndNeverStoreIt()
    {
        // Arrange
        var hash = EnrollmentKeys.Hash("Safety-2026");

        // Act & Assert
        Assert.DoesNotContain("Safety-2026", hash, StringComparison.Ordinal);
        Assert.True(EnrollmentKeys.Verify("Safety-2026", hash));
        Assert.True(EnrollmentKeys.Verify("  Safety-2026 ", hash));
        Assert.False(EnrollmentKeys.Verify("safety-2026", hash));
        Assert.False(EnrollmentKeys.Verify(null, hash));
        Assert.False(EnrollmentKeys.Verify("Safety-2026", null));
        Assert.NotEqual(hash, EnrollmentKeys.Hash("Safety-2026"));
    }
}
