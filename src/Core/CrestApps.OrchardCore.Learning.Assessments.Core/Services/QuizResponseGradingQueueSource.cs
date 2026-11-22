using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Lists the quiz attempts of learners that wait for a grader, for the Learning grading page; each opens on the Surveys
/// grading page.
/// </summary>
public sealed class QuizResponseGradingQueueSource : IGradingQueueSource
{
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly ISurveyManager _surveyManager;
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizResponseGradingQueueSource"/> class.
    /// </summary>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="contentManager">The content manager.</param>
    public QuizResponseGradingQueueSource(
        ISurveyResponseQueries responseQueries,
        ISurveyManager surveyManager,
        IContentManager contentManager)
    {
        _responseQueries = responseQueries;
        _surveyManager = surveyManager;
        _contentManager = contentManager;
    }

    /// <inheritdoc />
    public async Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CourseIds is { Count: 0 })
        {
            return new GradingQueuePage();
        }

        var take = Math.Clamp(query.Take, 1, SurveysConstants.PageSize);
        var (_, responses) = await _responseQueries.ListAsync(new SurveyResponseQuery
        {
            Statuses = [SurveyResponseStatus.Completed],
            Grading = GradingStatus.Pending,
            ContextKinds = [LearningSurveyContexts.EnrollmentKind],
            Take = SurveysConstants.PageSize,
        }, cancellationToken);

        var items = new List<GradingQueueItem>();

        foreach (var response in responses)
        {
            if (response.Context is not { } context || !string.Equals(context.Kind, LearningSurveyContexts.EnrollmentKind, StringComparison.Ordinal))
            {
                continue;
            }

            var courseId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseIdProperty);

            if (string.IsNullOrEmpty(courseId) || (query.CourseIds is not null && !query.CourseIds.Contains(courseId)))
            {
                continue;
            }

            var itemId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseItemIdProperty);
            var title = string.IsNullOrEmpty(itemId) ? null : (await _contentManager.GetAsync(itemId, VersionOptions.Latest))?.DisplayText;

            title ??= (await _surveyManager.FindByIdAsync(response.SurveyId, cancellationToken))?.Title;

            items.Add(new GradingQueueItem
            {
                Kind = AssessmentsConstants.QuizResponseGradingQueueKind,
                Id = response.ItemId,
                UserId = response.UserId,
                CourseId = courseId,
                ItemId = itemId,
                ItemTitle = title,
                Reference = response.Number,
                Icon = "fa-solid fa-circle-question",
                SubmittedUtc = response.CompletedUtc ?? response.StartedUtc,
                Area = SurveysConstants.Features.Surveys,
                Controller = "SurveyGrading",
                Action = "Grade",
                RouteValues = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["responseId"] = response.ItemId,
                },
            });
        }

        return new GradingQueuePage
        {
            Count = items.Count,
            Items = items.Take(take).ToArray(),
        };
    }
}
