using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.AuditTrail;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

/// <summary>
/// The audit trail keeps the request's IP address next to the response id, so on an anonymous survey the events the
/// respondent causes are not recorded, while staff events still are.
/// </summary>
public sealed class SurveysAuditTrailObserverTests
{
    [Fact]
    public async Task ReopenedAsync_ByTheRespondentOfAnAnonymousSurvey_IsNotRecorded()
    {
        // Arrange
        var auditTrail = new Mock<ISurveyAuditTrail>();
        var observer = new SurveysAuditTrailObserver(auditTrail.Object);

        // Act: a respondent's reopen has no actor.
        await observer.ReopenedAsync(Context(AnonymityMode.Anonymous, actorId: null));

        // Assert
        auditTrail.Verify(trail => trail.RecordAsync(It.IsAny<string>(), It.IsAny<SurveyAuditEntry>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ReopenedAsync_ByTheRespondentOfAnIdentifiedSurvey_IsRecorded()
    {
        // Arrange
        var auditTrail = new Mock<ISurveyAuditTrail>();
        var observer = new SurveysAuditTrailObserver(auditTrail.Object);

        // Act
        await observer.ReopenedAsync(Context(AnonymityMode.Identified, actorId: null));

        // Assert
        auditTrail.Verify(trail => trail.RecordAsync(SurveyAuditEvents.ResponseReopened, It.Is<SurveyAuditEntry>(entry => entry.ResponseId == "r1"), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task VoidedAsync_ByStaffOnAnAnonymousSurvey_IsStillRecorded()
    {
        // Arrange
        var auditTrail = new Mock<ISurveyAuditTrail>();
        var observer = new SurveysAuditTrailObserver(auditTrail.Object);

        // Act
        await observer.VoidedAsync(Context(AnonymityMode.Anonymous, actorId: "admin-id"));

        // Assert
        auditTrail.Verify(trail => trail.RecordAsync(SurveyAuditEvents.ResponseVoided, It.Is<SurveyAuditEntry>(entry => entry.ActorId == "admin-id"), It.IsAny<CancellationToken>()), Times.Once());
    }

    private static SurveyResponseEventContext Context(AnonymityMode anonymity, string actorId)
        => new()
        {
            Survey = new Survey { ItemId = "s1", Name = "s1", Access = new SurveyAccessSettings { Anonymity = anonymity } },
            Response = new SurveyResponse { ItemId = "r1", SurveyId = "s1" },
            ActorId = actorId,
        };
}
