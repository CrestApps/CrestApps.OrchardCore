using CrestApps.OrchardCore.Learning.AuditTrail;
using CrestApps.OrchardCore.Learning.Models;
using Moq;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Only the changes a person makes with a reason, and exports of learner records, become Audit Trail events.
/// </summary>
public sealed class LearningAuditTrailObserverTests
{
    [Fact]
    public async Task ChangesByAPersonWithAReason_AreRecorded_ChangesLearningMakesOnItsOwn_AreNot()
    {
        // Arrange
        var recorded = new List<AuditTrailContext<LearningAuditData>>();
        var manager = new Mock<IAuditTrailManager>();
        manager.Setup(m => m.RecordEventAsync(It.IsAny<AuditTrailContext<LearningAuditData>>()))
            .Callback<AuditTrailContext<LearningAuditData>>(recorded.Add)
            .Returns(Task.CompletedTask);
        var observer = new LearningAuditTrailObserver(manager.Object);
        var byHand = Enrollment(new EnrollmentStatusChange { From = EnrollmentStatus.Active, To = EnrollmentStatus.Suspended, Actor = "manager", Reason = "On leave" });
        var onItsOwn = Enrollment(new EnrollmentStatusChange { From = EnrollmentStatus.Active, To = EnrollmentStatus.Completed, Actor = "learner" });

        // Act
        await observer.StatusChangedAsync(byHand, EnrollmentStatus.Active);
        await observer.StatusChangedAsync(onItsOwn, EnrollmentStatus.Active);
        await observer.ItemCompletedAsync(byHand, new CourseItemProgress { ContentItemId = "lesson", CompletedBy = "manager", CompletionReason = "Done on paper" });
        await observer.ItemCompletedAsync(byHand, new CourseItemProgress { ContentItemId = "lesson2" });
        await observer.ExtendedAsync(byHand, "manager", "More time");
        await observer.ExportedAsync("LearningEnrollments", 12, "admin", "course1");

        // Assert
        Assert.Equal(
            [LearningAuditTrailEvents.StatusChanged, LearningAuditTrailEvents.ItemCompletedByHand, LearningAuditTrailEvents.AccessChanged, LearningAuditTrailEvents.LearnerRecordsExported],
            recorded.Select(context => context.Name));
        Assert.Equal("On leave", recorded[0].AuditTrailEventItem.Reason);
        Assert.Equal("Suspended", recorded[0].AuditTrailEventItem.To);
        Assert.Equal("lesson", recorded[1].AuditTrailEventItem.ItemId);
        Assert.Equal("manager", recorded[0].UserId);
    }

    private static Enrollment Enrollment(EnrollmentStatusChange change)
    {
        var enrollment = new Enrollment { ItemId = "e1", Number = "E000001", UserId = "learner", CourseId = "course1", Status = change.To };
        enrollment.History.Add(change);

        return enrollment;
    }
}
