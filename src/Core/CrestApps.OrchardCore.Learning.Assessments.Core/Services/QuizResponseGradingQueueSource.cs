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
/// grading page. The courses are turned into the surveys their course items own (the latest version of every item the
/// courses contain, so attempts of unpublished courses and items stay listed), so the query itself keeps only those
/// courses' attempts, oldest first, and the count is the count of all of them.
/// </summary>
public sealed class QuizResponseGradingQueueSource : IGradingQueueSource
{
    /// <summary>
    /// The most attempts read for one page of the grading list (the items before the page and the page itself): a page
    /// deeper than this shows nothing rather than reading every waiting attempt.
    /// </summary>
    public const int MaxTake = 1000;

    /// <summary>
    /// The most survey ids in one query's <c>IN</c> list, well under SQL Server's limit of 2,100 parameters; more are
    /// read in several queries and merged.
    /// </summary>
    public const int SurveysPerQuery = 1000;

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

        var titles = new Dictionary<string, string>(StringComparer.Ordinal);
        var take = Math.Clamp(query.Take, 1, MaxTake);
        int count;
        List<SurveyResponse> responses;

        if (query.CourseIds is null)
        {
            (count, responses) = await ReadOldestAsync([], take, cancellationToken);
        }
        else
        {
            var surveys = await GetCourseSurveysAsync(query.CourseIds, cancellationToken);

            if (surveys.Count == 0)
            {
                return new GradingQueuePage();
            }

            foreach (var survey in surveys)
            {
                titles[survey.ItemId] = survey.Title;
            }

            // Each read names at most SurveysPerQuery surveys. The oldest `take` attempts of all of them are among the
            // oldest `take` of each read, so those are merged and cut again.
            count = 0;
            responses = [];

            foreach (var chunk in surveys.Select(survey => survey.ItemId).Distinct(StringComparer.Ordinal).Chunk(SurveysPerQuery))
            {
                var (total, page) = await ReadOldestAsync([.. chunk], take, cancellationToken);

                count += total;
                responses.AddRange(page);
            }

            responses = [.. responses
                .OrderBy(response => response.CompletedUtc)
                .ThenBy(response => response.StartedUtc)
                .ThenBy(response => response.ItemId, StringComparer.Ordinal)
                .Take(take)];
        }

        var itemIds = responses
            .Select(response => SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseItemIdProperty))
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var itemTitles = itemIds.Length == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await _contentManager.GetAsync(itemIds, VersionOptions.Latest) ?? [])
                .Where(item => item is not null)
                .GroupBy(item => item.ContentItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().DisplayText, StringComparer.Ordinal);

        var items = new List<GradingQueueItem>(responses.Count);

        foreach (var response in responses)
        {
            var courseId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseIdProperty);
            var itemId = SurveyResponseProperties.ReadString(response, LearningSurveyContexts.CourseItemIdProperty);
            var title = string.IsNullOrEmpty(itemId) ? null : itemTitles.GetValueOrDefault(itemId);

            if (title is null && !titles.TryGetValue(response.SurveyId, out title))
            {
                title = (await _surveyManager.FindByIdAsync(response.SurveyId, cancellationToken))?.Title;
                titles[response.SurveyId] = title;
            }

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

    // Reads the oldest `take` waiting attempts of some surveys (all surveys when none are named), a read at a time, and
    // the count of all of them.
    private async Task<(int Count, List<SurveyResponse> Responses)> ReadOldestAsync(List<string> surveyIds, int take, CancellationToken cancellationToken)
    {
        var count = 0;
        var responses = new List<SurveyResponse>();

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

        return (count, responses);
    }

    private async Task<IReadOnlyList<Survey>> GetCourseSurveysAsync(IReadOnlyCollection<string> courseIds, CancellationToken cancellationToken)
    {
        var owners = (await _courseStructure.GetContainedItemsAsync(courseIds))
            .Where(item => item is not null)
            .Select(item => item.ContentItemId)
            .Distinct(StringComparer.Ordinal)
            .Select(LearningSurveyContexts.Owner)
            .ToArray();

        return owners.Length == 0
            ? []
            : await _surveyManager.ListByOwnersAsync(owners, cancellationToken) ?? [];
    }
}
