using CrestApps.OrchardCore.Learning.Assessments.AuditTrail;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Moq;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Grading by a person, every score change and every quiz override become Audit Trail events; automatic scoring does not.
/// </summary>
public sealed class AssessmentsAuditTrailObserverTests
{
    [Fact]
    public async Task PeoplesGradingChangesAndOverrides_AreRecorded_AutomaticScoringIsNot()
    {
        // Arrange
        var recorded = new List<AuditTrailContext<AssessmentsAuditData>>();
        var manager = new Mock<IAuditTrailManager>();
        manager.Setup(m => m.RecordEventAsync(It.IsAny<AuditTrailContext<AssessmentsAuditData>>()))
            .Callback<AuditTrailContext<AssessmentsAuditData>>(recorded.Add)
            .Returns(Task.CompletedTask);
        var observer = new AssessmentsAuditTrailObserver(manager.Object);
        var automatic = new AssessmentAttempt { ItemId = "a1", UserId = "learner", QuizId = "quiz", Score = 3 };
        var graded = new AssessmentAttempt { ItemId = "a2", UserId = "learner", QuizId = "quiz", Score = 4, GradedBy = "grader" };

        // Act
        await observer.GradedAsync(automatic);
        await observer.GradedAsync(graded);
        await observer.ScoreChangedAsync(graded, new AttemptScoreChange { Kind = "Voided", Actor = "manager", From = 4, Reason = "Copied answers" });
        await observer.OverrideGrantedAsync(new AssessmentOverride { ItemId = "o1", UserId = "learner", QuizId = "quiz", ExtraMinutes = 15, Actor = "manager", Reason = "Reading support" });

        // Assert
        Assert.Equal([AssessmentsAuditTrailEvents.Graded, AssessmentsAuditTrailEvents.ScoreChanged, AssessmentsAuditTrailEvents.OverrideGranted], recorded.Select(context => context.Name));
        Assert.Equal("grader", recorded[0].UserId);
        Assert.Equal("Copied answers", recorded[1].AuditTrailEventItem.Reason);
        Assert.Equal("Reading support", recorded[2].AuditTrailEventItem.Reason);
    }
}
