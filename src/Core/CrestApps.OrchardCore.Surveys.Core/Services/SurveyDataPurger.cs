using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Removes the records a survey leaves behind when it is deleted: its test responses (with their files), invitations
/// (which hold e-mail addresses and names), accommodations, participation counts and counters. Each call removes one
/// batch on the scope's own session and commits it, so a caller runs it a batch at a time in child scopes.
/// </summary>
public sealed class SurveyDataPurger
{
    /// <summary>
    /// The number of records one batch removes at most.
    /// </summary>
    public const int BatchSize = 200;

    private readonly ISession _session;
    private readonly SurveyResponseFiles _files;
    private readonly bool _hasCounters;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDataPurger"/> class.
    /// </summary>
    /// <param name="session">The YesSql session of the scope.</param>
    /// <param name="files">The response files, to release the files of the deleted test responses.</param>
    /// <param name="counterStores">The counter store, registered (with its table) only by the feature that keeps counters.</param>
    public SurveyDataPurger(ISession session, SurveyResponseFiles files, IEnumerable<SurveyCounterStore> counterStores)
    {
        _session = session;
        _files = files;
        _hasCounters = counterStores?.Any() == true;
    }

    /// <summary>
    /// Removes up to <paramref name="batchSize"/> records of a survey and commits them. The files of the removed test
    /// responses are released after the commit, so a failed save never loses a file of a response that stays.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="batchSize">The number of records to remove at most.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many records were removed; fewer than <paramref name="batchSize"/> once nothing is left.</returns>
    public async Task<int> DeleteBatchAsync(string surveyId, int batchSize = BatchSize, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(surveyId);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var left = batchSize;

        var responses = (await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .OrderBy(index => index.Id)
            .Take(left)
            .ListAsync(cancellationToken)).ToList();

        foreach (var response in responses)
        {
            _session.Delete(response, SurveysConstants.ResponsesCollectionName);
        }

        left -= responses.Count;
        left -= await DeleteAsync<SurveyInvitation, SurveyInvitationIndex>(index => index.SurveyId == surveyId, left, cancellationToken);
        left -= await DeleteAsync<SurveyOverride, SurveyOverrideIndex>(index => index.SurveyId == surveyId, left, cancellationToken);
        left -= await DeleteAsync<SurveyParticipation, SurveyParticipationIndex>(index => index.SurveyId == surveyId, left, cancellationToken);

        if (_hasCounters)
        {
            left -= await DeleteAsync<SurveyCounter, SurveyCounterIndex>(index => index.SurveyId == surveyId, left, cancellationToken);
        }


        await _session.SaveChangesAsync(cancellationToken);

        foreach (var response in responses)
        {
            await _files.DeleteAllAsync(response, cancellationToken);
        }

        return batchSize - left;
    }

    private async Task<int> DeleteAsync<TDocument, TIndex>(System.Linq.Expressions.Expression<Func<TIndex, bool>> predicate, int take, CancellationToken cancellationToken)
        where TDocument : class
        where TIndex : CrestApps.Core.Data.YesSql.Indexes.CatalogItemIndex
    {
        if (take <= 0)
        {
            return 0;
        }

        var items = await _session.Query<TDocument, TIndex>(predicate, collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);

        var count = 0;

        foreach (var item in items)
        {
            _session.Delete(item, SurveysConstants.CollectionName);
            count++;
        }

        return count;
    }
}
