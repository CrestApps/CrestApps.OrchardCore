using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Records what observers hear about surveys and responses in the audit trail. Test responses are left out.
/// </summary>
internal sealed class SurveysAuditTrailObserver : ISurveyResponseObserver, ISurveyObserver
{
    private readonly ISurveyAuditTrail _auditTrail;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAuditTrailObserver"/> class.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    public SurveysAuditTrailObserver(ISurveyAuditTrail auditTrail)
    {
        _auditTrail = auditTrail;
    }

    /// <inheritdoc />
    public Task PublishedAsync(Survey survey, SurveyVersion version)
        => _auditTrail.RecordAsync(SurveyAuditEvents.SurveyPublished, new SurveyAuditEntry
        {
            ActorId = version?.PublishedBy,
            SurveyId = survey?.ItemId,
            SurveyName = survey?.Name,
            VersionNumber = version?.Number,
            Detail = version?.Notes,
        });

    /// <inheritdoc />
    public Task StatusChangedAsync(Survey survey, SurveyStatus previous)
    {
        var eventName = survey?.Status switch
        {
            SurveyStatus.Closed => SurveyAuditEvents.SurveyClosed,
            SurveyStatus.Archived => SurveyAuditEvents.SurveyArchived,
            _ => null,
        };

        return eventName is null
            ? Task.CompletedTask
            : _auditTrail.RecordAsync(eventName, Entry(survey));
    }

    /// <inheritdoc />
    public Task DeletedAsync(Survey survey)
        => _auditTrail.RecordAsync(SurveyAuditEvents.SurveyDeleted, Entry(survey));

    /// <inheritdoc />
    public Task VoidedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ResponseVoided, context);

    /// <inheritdoc />
    public Task DeletedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ResponseDeleted, context);

    /// <inheritdoc />
    public Task ReopenedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ResponseReopened, context);

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ScoreChanged, context);

    /// <inheritdoc />
    public Task OverrideGrantedAsync(SurveyOverride surveyOverride)
        => surveyOverride is null
            ? Task.CompletedTask
            : _auditTrail.RecordAsync(SurveyAuditEvents.OverrideGranted, new SurveyAuditEntry
            {
                ActorId = surveyOverride.Actor,
                SurveyId = surveyOverride.SurveyId,
                Reason = surveyOverride.Reason,
                Detail = $"+{surveyOverride.ExtraMinutes} min, +{surveyOverride.ExtraAttempts} attempts",
            });

    private static SurveyAuditEntry Entry(Survey survey)
        => new()
        {
            SurveyId = survey?.ItemId,
            SurveyName = survey?.Name,
        };

    private Task Response(string eventName, SurveyResponseEventContext context)
    {
        var response = context?.Response;

        if (response is null || response.IsTest)
        {
            return Task.CompletedTask;
        }

        return _auditTrail.RecordAsync(eventName, new SurveyAuditEntry
        {
            ActorId = context.CausedByWorkflow ? "Workflow" : context.ActorId,
            SurveyId = response.SurveyId,
            SurveyName = context.Survey?.Name,
            ResponseId = response.ItemId,
            ResponseNumber = response.Number,
            VersionNumber = response.VersionNumber,
            From = context.ScoreChange?.From,
            To = context.ScoreChange?.To ?? response.Score,
            Reason = context.ScoreChange?.Reason,
        });
    }
}
