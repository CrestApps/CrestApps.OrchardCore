using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Lists the quiz attempts of learners that wait for a grader, for the Learning grading page; each opens on the Surveys
/// grading page. The courses are turned into the surveys their course items own, so the query itself keeps only those
/// courses' attempts, oldest first, and the count is the count of all of them.
/// </summary>
public sealed class QuizResponseGradingQueueSource : IGradingQueueSource
{
    private readonly ISurveyResponseQueries _responseQueries;
    private readonly ISurveyManager _surveyManager;
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizResponseGradingQueueSource"/> class.
    /// </summary>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="courseStructure">The course structure service.</param>
    public QuizResponseGradingQueueSource(
        ISurveyResponseQueries responseQueries,
        ISurveyManager surveyManager,
        IContentManager contentManager,
        ICourseStructureService courseStructure)
    {
        _responseQueries = responseQueries;
        _surveyManager = surveyManager;
        _contentManager = contentManager;
        _courseStructure = courseStructure;
    }

    /// <inheritdoc />
    public async Task<GradingQueuePage> ListWaitingAsync(GradingQueueQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CourseIds is { Count: 0 })
        {
            return new GradingQueuePage();
        }

        List<string> surveyIds = [];

        if (query.CourseIds is not null)
        {
            surveyIds = await GetCourseSurveyIdsAsync(query.CourseIds, cancellationToken);

            if (surveyIds.Count == 0)
            {
                return new GradingQueuePage();
            }
        }

        var take = Math.Max(query.Take, 1);
        var count = 0;
        var responses = new List<SurveyResponse>();

        // The grading list asks for the items before its page and the page itself, which can be more than one read
        // returns; read the oldest pages until there are enough.
        while (responses.Count < take)
        {
            var (total, page) = await _responseQueries.ListAsync(new SurveyResponseQuery
            {
                SurveyIds = surveyIds,
                Statuses = [SurveyResponseStatus.Completed],
                Grading = GradingStatus.Pending,
                ContextKinds = [LearningSurveyContexts.EnrollmentKind],
                OldestFirst = true,
                Skip = responses.Count,
                Take = Math.Min(take - responses.Count, SurveysConstants.PageSize),
            }, cancellationToken);

            count = total;
            responses.AddRange(page);

            if (page.Count == 0 || responses.Count >= total)
            {
                break;
            }
        }

        var items = new List<GradingQueueItem>(responses.Count);

        foreach (var response in responses)
        {
            var courseId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseIdProperty);
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
            Count = count,
            Items = items,
        };
    }

    private async Task<List<string>> GetCourseSurveyIdsAsync(IReadOnlyCollection<string> courseIds, CancellationToken cancellationToken)
    {
        var surveyIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var courseId in courseIds)
        {
            var course = await _courseStructure.GetCourseAsync(courseId);

            if (course is null)
            {
                continue;
            }

            var outline = await _courseStructure.GetOutlineAsync(course);

            foreach (var entry in outline.Entries)
            {
                if (entry.Item is null)
                {
                    continue;
                }

                var surveys = await _surveyManager.ListByOwnerAsync(LearningSurveyContexts.Owner(entry.Item.ContentItemId), cancellationToken);

                foreach (var survey in surveys ?? [])
                {
                    surveyIds.Add(survey.ItemId);
                }
            }
        }

        return [.. surveyIds];
    }
}
