using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Declares the Surveys audit trail events.
/// </summary>
internal sealed class SurveysAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    /// <inheritdoc />
    public void Configure(AuditTrailOptions options)
        => options.For<SurveysAuditTrailEvents>(SurveyAuditEvents.Category, S => S["Surveys"])
            .WithEvent(SurveyAuditEvents.SurveyCreated, S => S["Survey created"], S => S["A survey was created."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.SurveyPublished, S => S["Survey published"], S => S["A survey version was published."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.SurveyRestored, S => S["Survey version restored"], S => S["An older version was copied into a survey's draft."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.SurveyClosed, S => S["Survey closed"], S => S["A survey stopped collecting responses."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.SurveyArchived, S => S["Survey archived"], S => S["A survey was archived."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.SurveyDeleted, S => S["Survey deleted"], S => S["A survey was deleted."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.AccessChanged, S => S["Survey access changed"], S => S["Who may respond to a survey, or when, changed."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.ResponsesExported, S => S["Survey responses exported"], S => S["Survey responses were exported."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.ResponseVoided, S => S["Survey response voided"], S => S["A survey response was voided."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.ResponseDeleted, S => S["Survey response deleted"], S => S["A survey response was deleted."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.ResponseReopened, S => S["Survey response reopened"], S => S["A completed survey response was reopened."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.ScoreChanged, S => S["Survey score changed"], S => S["The score of a survey response was regraded or changed, with a reason."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.OverrideGranted, S => S["Survey accommodation granted"], S => S["A respondent was given extra time, extra attempts or another window."], enableByDefault: true)
            .WithEvent(SurveyAuditEvents.OverrideRevoked, S => S["Survey accommodation revoked"], S => S["An accommodation was revoked."], enableByDefault: true);
}
