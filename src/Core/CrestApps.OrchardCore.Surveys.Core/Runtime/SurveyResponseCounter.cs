using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Counts responses through the response index.
/// </summary>
public sealed class SurveyResponseCounter
{
    private readonly SurveyResponseStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseCounter"/> class.
    /// </summary>
    /// <param name="store">The response store.</param>
    public SurveyResponseCounter(SurveyResponseStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Counts the completed responses of a survey, test responses left out.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="collectorId">The collector, or <see langword="null"/> for all.</param>
    /// <returns>The count.</returns>
    public Task<int> CountCompletedAsync(string surveyId, string collectorId)
    {
        var completed = nameof(SurveyResponseStatus.Completed);

        return collectorId is null
            ? _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Status == completed && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync()
            : _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.CollectorId == collectorId && index.Status == completed && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync();
    }

    /// <summary>
    /// Counts the responses of a user in a context that were not voided, test responses left out.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user.</param>
    /// <param name="contextRef">The context as Kind:Id, or <see langword="null"/>.</param>
    /// <returns>The count.</returns>
    public Task<int> CountForUserAsync(string surveyId, string userId, string contextRef)
    {
        var voided = nameof(SurveyResponseStatus.Voided);

        return contextRef is null
            ? _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.Status != voided && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync()
            : _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.ContextRef == contextRef && index.Status != voided && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync();
    }

    /// <summary>
    /// Counts the completed responses of a user in a context (screened-out, abandoned, voided and test responses left
    /// out, and a reopened response until it completes again).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user.</param>
    /// <param name="contextRef">The context as Kind:Id, or <see langword="null"/>.</param>
    /// <returns>The count.</returns>
    public Task<int> CountCompletedForUserAsync(string surveyId, string userId, string contextRef)
    {
        var completed = nameof(SurveyResponseStatus.Completed);

        return contextRef is null
            ? _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.Status == completed && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync()
            : _store.YesSqlSession.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.ContextRef == contextRef && index.Status == completed && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).CountAsync();
    }

    /// <summary>
    /// Finds the response in progress of a user for a survey and context.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The user.</param>
    /// <param name="contextRef">The context as Kind:Id, or <see langword="null"/>.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    public Task<SurveyResponse> FindInProgressAsync(string surveyId, string userId, string contextRef)
    {
        var inProgress = nameof(SurveyResponseStatus.InProgress);

        return contextRef is null
            ? _store.YesSqlSession.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.ContextRef == null && index.Status == inProgress && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync()
            : _store.YesSqlSession.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.UserId == userId && index.ContextRef == contextRef && index.Status == inProgress && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Finds the response in progress started from a personal invitation, test responses left out.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="invitationId">The invitation.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    public Task<SurveyResponse> FindInProgressByInvitationAsync(string surveyId, string invitationId)
    {
        if (string.IsNullOrEmpty(surveyId) || string.IsNullOrEmpty(invitationId))
        {
            return Task.FromResult<SurveyResponse>(null);
        }

        var inProgress = nameof(SurveyResponseStatus.InProgress);

        return _store.YesSqlSession.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.InvitationId == invitationId && index.Status == inProgress && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Lists the responses of a user that are not tests or voided, newest first.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="take">The most to read.</param>
    /// <returns>The responses.</returns>
    public async Task<IReadOnlyList<SurveyResponse>> ListForUserAsync(string userId, int take)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var voided = nameof(SurveyResponseStatus.Voided);
        var items = await _store.YesSqlSession.Query<SurveyResponse, SurveyResponseIndex>(index => index.UserId == userId && index.Status != voided && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .OrderByDescending(index => index.StartedUtc)
            .Take(take)
            .ListAsync();

        return items.ToList();
    }
}
