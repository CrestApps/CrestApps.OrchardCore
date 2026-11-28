using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stores participation counts for respondent limits.
/// </summary>
public sealed class SurveyParticipationStore : DocumentCatalog<SurveyParticipation, SurveyParticipationIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyParticipationStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyParticipationStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <inheritdoc />
    protected override ValueTask SavingAsync(SurveyParticipation record)
    {
        // An anonymous respondent's record keeps the day only, never the exact time: every save clears it, so a record
        // written before that rule loses it on any save, not only through the upgrade step.
        if (record is not null && SurveyRespondentKeys.IsAnonymous(record.RespondentKey))
        {
            record.LastCompletedUtc = null;
        }

        return base.SavingAsync(record);
    }

    /// <summary>
    /// Lists the participation rows of a respondent from a day on.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="respondentKey">The respondent key.</param>
    /// <param name="fromDay">The first day.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public async Task<IReadOnlyList<SurveyParticipation>> ListAsync(string surveyId, string respondentKey, DateOnly fromDay, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(respondentKey))
        {
            return [];
        }

        var from = fromDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var items = await Session.Query<SurveyParticipation, SurveyParticipationIndex>(
            index => index.SurveyId == surveyId && index.RespondentKey == respondentKey && index.Day >= from,
            collection: SurveysConstants.CollectionName)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <summary>
    /// Gets the YesSql session, for queries of the core services.
    /// </summary>
    internal ISession YesSqlSession => Session;
}
