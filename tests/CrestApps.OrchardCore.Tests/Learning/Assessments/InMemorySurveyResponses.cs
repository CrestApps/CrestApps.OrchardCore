using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Quiz attempts (survey responses) the Learning tests add by hand, read through <see cref="ISurveyResponseQueries"/>,
/// and the observers told about them as the Surveys runtime tells them after a save.
/// </summary>
internal sealed class InMemorySurveyResponses : ISurveyResponseQueries
{
    private readonly List<SurveyResponse> _responses = [];

    /// <summary>
    /// Gets every query <see cref="ListAsync"/> was asked, in order.
    /// </summary>
    public List<SurveyResponseQuery> Queries { get; } = [];

    /// <summary>
    /// Adds a response as it is.
    /// </summary>
    /// <param name="response">The response.</param>
    public void Add(SurveyResponse response)
        => _responses.Add(response);

    public Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default)
        => Task.FromResult(_responses.FirstOrDefault(response => response.ItemId == responseId));

    public Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);

        var matches = _responses
            .Where(response => query.SurveyIds.Count == 0 || query.SurveyIds.Contains(response.SurveyId))
            .Where(response => query.UserId is null || response.UserId == query.UserId)
            .Where(response => query.ContextRef is null || response.Context?.ToString() == query.ContextRef)
            .Where(response => query.Statuses.Count == 0 || query.Statuses.Contains(response.Status))
            .Where(response => query.Grading is null || response.Grading == query.Grading)
            .Where(response => query.ContextKinds.Count == 0 || (response.Context is { } context && query.ContextKinds.Contains(context.Kind)))
            .ToList();

        matches = query.OldestFirst
            ? [.. matches.OrderBy(response => response.CompletedUtc).ThenBy(response => response.StartedUtc).ThenBy(response => response.ItemId, StringComparer.Ordinal)]
            : [.. matches.OrderByDescending(response => response.StartedUtc)];

        return Task.FromResult<(int, IReadOnlyList<SurveyResponse>)>((matches.Count, matches.Skip(query.Skip).Take(Math.Clamp(query.Take, 1, SurveysConstants.PageSize)).ToList()));
    }

    public Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
        => Task.FromResult(_responses.Count(response => response.SurveyId == surveyId && !response.IsTest));

    /// <summary>
    /// Adds a completed attempt through an enrollment and tells the observers it was completed.
    /// </summary>
    /// <param name="services">The services of a scope.</param>
    /// <param name="quizId">The quiz course item that owns the survey.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="userId">The learner.</param>
    /// <param name="enrollmentId">The enrollment.</param>
    /// <param name="completedUtc">When the attempt ended.</param>
    /// <param name="percent">The score as a percent.</param>
    /// <param name="passed">Whether it passed.</param>
    /// <returns>The attempt.</returns>
    public async Task<SurveyResponse> CompleteAsync(IServiceProvider services, string quizId, string surveyId, string userId, string enrollmentId, DateTime completedUtc, decimal percent, bool passed)
    {
        var response = new SurveyResponse
        {
            ItemId = "response-" + (_responses.Count + 1),
            SurveyId = surveyId,
            UserId = userId,
            Status = SurveyResponseStatus.Completed,
            Grading = GradingStatus.Graded,
            Context = LearningSurveyContexts.Enrollment(enrollmentId),
            StartedUtc = completedUtc,
            CompletedUtc = completedUtc,
            Percent = percent,
            Passed = passed,
        };

        _responses.Add(response);

        var context = new SurveyResponseEventContext
        {
            Survey = new Survey { ItemId = surveyId, Kind = SurveyKinds.Quiz, Owner = LearningSurveyContexts.Owner(quizId) },
            Response = response,
        };

        foreach (var observer in services.GetServices<ISurveyResponseObserver>())
        {
            await observer.CompletedAsync(context);
        }

        return response;
    }
}
