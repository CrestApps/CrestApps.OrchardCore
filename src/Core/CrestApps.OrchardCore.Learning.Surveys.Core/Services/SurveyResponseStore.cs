using CrestApps.OrchardCore.Learning.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Services;

/// <summary>
/// Stores survey responses in their own collection.
/// </summary>
public sealed class SurveyResponseStore : DocumentCatalog<SurveyResponse, SurveyResponseIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public SurveyResponseStore(ISession session)
        : base(session)
    {
        CollectionName = SurveysConstants.CollectionName;
    }

    /// <summary>
    /// Finds a learner's response to a survey, read again from the store so a write under the survey lock sees what
    /// the last writer committed.
    /// </summary>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    public async Task<SurveyResponse> FindForEnrollmentAsync(string enrollmentId, string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(enrollmentId) || string.IsNullOrEmpty(surveyId))
        {
            return null;
        }

        var tracked = await Session.Query<SurveyResponse, SurveyResponseIndex>(index => index.EnrollmentId == enrollmentId && index.SurveyId == surveyId, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        Session.Detach(tracked, CollectionName);

        return await FindByIdAsync(tracked.ItemId, cancellationToken);
    }

    /// <summary>
    /// Lists every response to a survey.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The responses.</returns>
    public async Task<IReadOnlyList<SurveyResponse>> ListBySurveyAsync(string surveyId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(surveyId)
            ? []
            : (await Session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: CollectionName)
                .OrderBy(index => index.SubmittedUtc)
                .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Lists the responses of a learner, newest first.
    /// </summary>
    /// <param name="userId">The learner.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The responses.</returns>
    public async Task<IReadOnlyList<SurveyResponse>> ListByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? []
            : (await Session.Query<SurveyResponse, SurveyResponseIndex>(index => index.UserId == userId, collection: CollectionName)
                .OrderByDescending(index => index.SubmittedUtc)
                .ListAsync(cancellationToken)).ToArray();

    /// <summary>
    /// Reads a page of the responses to a survey, newest first.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">The page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<SurveyResponse> Items)> PageBySurveyAsync(string surveyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return (0, []);
        }

        var query = Session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.SubmittedUtc).ThenByDescending(index => index.Id).Skip(Math.Max(skip, 0)).Take(Math.Max(take, 1)).ListAsync(cancellationToken);

        return (count, items.ToArray());
    }
}
