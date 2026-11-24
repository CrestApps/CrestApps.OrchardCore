using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Records what observers hear about surveys and responses in the audit trail. Test responses are left out, and so are
/// the events a respondent causes on an anonymous survey (a reopen), whose audit record would keep the respondent's IP
/// address next to the response. Events the system causes there (a retention delete, an automatic regrade) are still
/// recorded, with no actor and nothing that names the respondent.
/// </summary>
internal sealed class SurveysAuditTrailObserver : ISurveyResponseObserver, ISurveyObserver
{
    private readonly ISurveyAuditTrail _auditTrail;
    private readonly SurveyRespondentNames _names;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAuditTrailObserver"/> class.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    public SurveysAuditTrailObserver(ISurveyAuditTrail auditTrail, SurveyRespondentNames names)
    {
        _auditTrail = auditTrail;
        _names = names;
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
        => Response(SurveyAuditEvents.ResponseVoided, context, causedByRespondent: false);

    /// <inheritdoc />
    public Task DeletedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ResponseDeleted, context, causedByRespondent: false);

    /// <inheritdoc />
    public Task ReopenedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ResponseReopened, context, causedByRespondent: true);

    /// <inheritdoc />
    public Task ScoreChangedAsync(SurveyResponseEventContext context)
        => Response(SurveyAuditEvents.ScoreChanged, context, causedByRespondent: false);

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

    private async Task Response(string eventName, SurveyResponseEventContext context, bool causedByRespondent)
    {
        var response = context?.Response;

        if (response is null || response.IsTest)
        {
            return;
        }

        // On an anonymous survey, an event the respondent caused (a reopen with no actor and no workflow) is not recorded:
        // the audit trail keeps the request's IP address next to the response id, which would point to the respondent.
        // An event with no actor the system caused, such as a retention delete, is recorded: it names no one.
        if (causedByRespondent && SurveyAnonymity.IsAnonymous(context.Survey) && string.IsNullOrEmpty(context.ActorId) && !context.CausedByWorkflow)
        {
            return;
        }

        await _auditTrail.RecordAsync(eventName, new SurveyAuditEntry
        {
            ActorId = context.CausedByWorkflow ? "Workflow" : context.ActorId,
            SurveyId = response.SurveyId,
            SurveyName = context.Survey?.Name,
            ResponseId = response.ItemId,
            // No site-wide response number on an anonymous survey (see SurveyAnonymity.ResponseLabel).
            ResponseNumber = await _names.GetLabelAsync(context.Survey, response),
            VersionNumber = response.VersionNumber,
            From = context.ScoreChange?.From,
            To = context.ScoreChange?.To ?? response.Score,
            Reason = context.ScoreChange?.Reason,
        });
    }
}
