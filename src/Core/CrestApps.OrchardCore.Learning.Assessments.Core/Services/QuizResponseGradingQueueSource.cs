using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Lists the quiz attempts of learners that wait for a grader, for the Learning grading page; each opens on the Surveys
/// grading page. The courses are turned into the surveys their course items own (the latest version of every item the
/// courses contain, so attempts of unpublished courses and items stay listed), so the query itself keeps only those
/// courses' attempts, and the count is the count of all of them. The grader's own attempts are left out (nobody grades
/// their own work, <see cref="SurveyOwnWork"/>). An anonymous survey follows the shared small-group rule
/// (<see cref="SurveyAnonymousGroups"/>): nothing of it is listed while its completed responses are below the minimum
/// group size, and then its attempts (which belong to the course, not to an enrollment) are listed with no learner,
/// labeled by <see cref="SurveyRespondentNames.GetLabelAsync(SurveyResponse, CancellationToken)"/> rather than the
/// site-wide response number, and dated by the day only. Identified attempts are listed oldest first; anonymous ones by
/// their day, then their id, so none sits between two named attempts by its exact time.
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
    /// read in several queries and merged in the same order.
    /// </summary>
    public const int SurveysPerQuery = SurveyIdChunks.Size;

    private readonly ISurveyResponseQueries _responseQueries;
    private readonly ISurveyManager _surveyManager;
    private readonly IContentManager _contentManager;
    private readonly ICourseStructureService _courseStructure;
    private readonly IServiceProvider _serviceProvider;

    // The groups of the listed anonymous surveys too small to show, worked out with the hidden surveys of a page.
    private List<SurveyResponseGroup> _hiddenGroups = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizResponseGradingQueueSource"/> class.
    /// </summary>
    /// <param name="responseQueries">The response queries.</param>
    /// <param name="surveyManager">The survey manager.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="courseStructure">The course structure service.</param>
    /// <param name="serviceProvider">The services, for the small-group rule and the site's time zone when they are there.</param>
    public QuizResponseGradingQueueSource(
        ISurveyResponseQueries responseQueries,
        ISurveyManager surveyManager,
        IContentManager contentManager,
        ICourseStructureService courseStructure,
        IServiceProvider serviceProvider)
    {
        _responseQueries = responseQueries;
        _surveyManager = surveyManager;
        _contentManager = contentManager;
        _courseStructure = courseStructure;
        _serviceProvider = serviceProvider;
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
        var known = new Dictionary<string, Survey>(StringComparer.Ordinal);
        var take = Math.Clamp(query.Take, 1, MaxTake);
        var grader = string.IsNullOrEmpty(query.GraderUserId) ? null : query.GraderUserId;
        // The one definition of an anonymous response's day (the site's time zone), as the Surveys pages read it.
        var timeZone = _serviceProvider.GetService<SurveyAnonymousDays>() is { } days ? await days.TimeZoneAsync() : CalendarTimeZone(await SiteTimeZoneAsync());
        IReadOnlyList<Survey> candidates;

        if (query.CourseIds is null)
        {
            candidates = await PendingSurveysAsync(known, cancellationToken);
        }
        else
        {
            candidates = await GetCourseSurveysAsync(query.CourseIds, cancellationToken);

            foreach (var survey in candidates)
            {
                known[survey.ItemId] = survey;
                titles[survey.ItemId] = survey.Title;
            }
        }

        var hidden = await HiddenAnonymousSurveysAsync(candidates, cancellationToken);
        var visible = candidates.Where(survey => survey is not null && !hidden.Contains(survey.ItemId)).ToList();

        if (visible.Count == 0)
        {
            return new GradingQueuePage();
        }

        // Identified attempts come oldest first by their completion time. An anonymous attempt is dated by its day only,
        // so anonymous surveys are read apart and ordered by that day, then by id: the list never places an anonymous
        // attempt between two named ones by its exact time. Both reads give their first `take` items in the order the
        // grading list merges every source by (date, then id), so its pages never repeat or skip an attempt.
        var identified = visible.Where(survey => !SurveyAnonymity.IsAnonymous(survey)).Select(survey => survey.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var anonymous = visible.Where(SurveyAnonymity.IsAnonymous).Select(survey => survey.ItemId).Distinct(StringComparer.Ordinal).ToList();
        var (identifiedCount, identifiedResponses) = identified.Count == 0 ? (0, new List<SurveyResponse>()) : await ReadOldestAsync(identified, grader, take, cancellationToken);
        var (anonymousCount, anonymousResponses) = anonymous.Count == 0 ? (0, new List<SurveyResponse>()) : await ReadAnonymousAsync(anonymous, grader, take, timeZone, cancellationToken);
        var anonymousIds = anonymous.ToHashSet(StringComparer.Ordinal);
        var count = identifiedCount + anonymousCount;

        DateTime SubmittedOf(SurveyResponse response)
            => anonymousIds.Contains(response.SurveyId)
                ? SurveyAnonymity.StartOfDayUtc(response.CompletedUtc ?? response.StartedUtc, timeZone)
                : DateTime.SpecifyKind(response.CompletedUtc ?? response.StartedUtc, DateTimeKind.Utc);

        var responses = identifiedResponses
            .Concat(anonymousResponses)
            .OrderBy(SubmittedOf)
            .ThenBy(response => response.ItemId, StringComparer.Ordinal)
            .Take(take)
            .ToList();

        return await PageAsync(count, responses, known, titles, timeZone, anonymousIds, cancellationToken);
    }

    // Turns the attempts read into the page: titles, labels, and dates (by the day only for anonymous surveys).
    private async Task<GradingQueuePage> PageAsync(int count, List<SurveyResponse> responses, Dictionary<string, Survey> known, Dictionary<string, string> titles, TimeZoneInfo timeZone, HashSet<string> anonymousIds, CancellationToken cancellationToken)
    {
        DateTime SubmittedOf(SurveyResponse response)
            => anonymousIds.Contains(response.SurveyId)
                ? SurveyAnonymity.StartOfDayUtc(response.CompletedUtc ?? response.StartedUtc, timeZone)
                : DateTime.SpecifyKind(response.CompletedUtc ?? response.StartedUtc, DateTimeKind.Utc);

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
        var names = _serviceProvider.GetService<SurveyRespondentNames>();

        foreach (var response in responses)
        {
            if (!known.TryGetValue(response.SurveyId, out var owner))
            {
                owner = await _surveyManager.FindByIdAsync(response.SurveyId, cancellationToken);
                known[response.SurveyId] = owner;
            }

            var isAnonymous = owner is null || SurveyAnonymity.IsAnonymous(owner);

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
                UserId = isAnonymous ? null : response.UserId,
                CourseId = courseId,
                ItemId = itemId,
                ItemTitle = title,
                Reference = names is null ? (isAnonymous ? string.Empty : response.Number) : await names.GetLabelAsync(owner, response, cancellationToken),
                Icon = "fa-solid fa-circle-question",
                SubmittedUtc = isAnonymous ? SurveyAnonymity.StartOfDayUtc(response.CompletedUtc ?? response.StartedUtc, timeZone) : SubmittedOf(response),
                SubmittedDay = isAnonymous ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(response.CompletedUtc ?? response.StartedUtc, DateTimeKind.Utc), timeZone).ToString("d", CultureInfo.CurrentCulture) : null,
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

    // Reads the first `take` waiting attempts of some surveys, oldest first by completion time (ties by id), a page at a
    // time, and the count of all of them. Both an enrollment's attempts and an anonymous course quiz's attempts (which belong to the course only) are
    // read; the course item's grading list is where either is graded.
    private async Task<(int Count, List<SurveyResponse> Responses)> ReadOldestAsync(List<string> surveyIds, string grader, int take, CancellationToken cancellationToken)
        => await ReadAsync(surveyIds, grader, take, byId: false, null, null, cancellationToken);

    // Reads the first `take` waiting attempts of anonymous surveys in the order of their day (in the site's time zone),
    // then their id. The first `take` attempts by completion time end on some day D; every attempt of an earlier day is
    // among them, so the rest of the page is the first attempts of day D by id.
    private async Task<(int Count, List<SurveyResponse> Responses)> ReadAnonymousAsync(List<string> surveyIds, string grader, int take, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        var (count, first) = await ReadOldestAsync(surveyIds, grader, take, cancellationToken);

        DateTime DayOf(SurveyResponse response)
            => SurveyAnonymity.StartOfDayUtc(response.CompletedUtc ?? response.StartedUtc, timeZone);

        if (first.Count < take || first.Count >= count)
        {
            return (count, [.. first.OrderBy(DayOf).ThenBy(response => response.ItemId, StringComparer.Ordinal)]);
        }

        var lastDay = DayOf(first[^1]);
        var before = first.Where(response => DayOf(response) < lastDay).OrderBy(DayOf).ThenBy(response => response.ItemId, StringComparer.Ordinal).ToList();
        var (_, sameDay) = await ReadAsync(surveyIds, grader, take - before.Count, byId: true, lastDay, SurveyAnonymity.EndOfDayUtc(lastDay, timeZone), cancellationToken);

        return (count, [.. before, .. sameDay]);
    }

    private async Task<(int Count, List<SurveyResponse> Responses)> ReadAsync(List<string> surveyIds, string grader, int take, bool byId, DateTime? completedFromUtc, DateTime? completedToUtc, CancellationToken cancellationToken)
    {
        if (surveyIds.Count > SurveysPerQuery)
        {
            // Each read names at most SurveysPerQuery surveys. The first `take` attempts of all of them are among the first
            // `take` of each read, so those are merged in the same order (ties by id) and cut again.
            var total = 0;
            var merged = new List<SurveyResponse>();

            foreach (var chunk in surveyIds.Chunk(SurveysPerQuery))
            {
                var (chunkCount, chunkResponses) = await ReadAsync([.. chunk], grader, take, byId, completedFromUtc, completedToUtc, cancellationToken);

                total += chunkCount;
                merged.AddRange(chunkResponses);
            }

            IEnumerable<SurveyResponse> ordered = byId
                ? merged.OrderBy(response => response.ItemId, StringComparer.Ordinal)
                : merged
                    .OrderBy(response => response.CompletedUtc ?? DateTime.MinValue)
                    .ThenBy(response => response.StartedUtc)
                    .ThenBy(response => response.ItemId, StringComparer.Ordinal);

            return (total, [.. ordered.Take(take)]);
        }

        var count = 0;
        var responses = new List<SurveyResponse>();

        while (responses.Count < take)
        {
            var (total, page) = await _responseQueries.ListAsync(new SurveyResponseQuery
            {
                SurveyIds = surveyIds,
                Statuses = [SurveyResponseStatus.Completed],
                Grading = GradingStatus.Pending,
                ContextKinds = [LearningSurveyContexts.EnrollmentKind, LearningSurveyContexts.CourseKind],
                ExcludedUserId = grader,
                HiddenGroups = [.. _hiddenGroups.Where(group => surveyIds.Contains(group.SurveyId, StringComparer.Ordinal))],
                OldestFirst = !byId,
                OrderById = byId,
                CompletedFromUtc = completedFromUtc,
                CompletedToUtc = completedToUtc,
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

    // The anonymous surveys whose completed responses are below the minimum group size: none of their attempts is
    // listed until there is a crowd to hide in, as on the Surveys grading queue. An attempt whose joint key (collector,
    // version, context kind) is in a group too small to show is left out too (SurveyAnonymousAttributes), as the Grade
    // page refuses it; more such groups than one query can leave out hide the whole survey.
    private async Task<HashSet<string>> HiddenAnonymousSurveysAsync(IEnumerable<Survey> surveys, CancellationToken cancellationToken)
    {
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        var groups = _serviceProvider.GetService<SurveyAnonymousGroups>();

        _hiddenGroups = [];

        foreach (var survey in surveys.Where(candidate => candidate is not null && SurveyAnonymity.IsAnonymous(candidate)))
        {
            if (groups is null || (await groups.DecideAsync(survey, null, [SurveyResponseStatus.Completed], cancellationToken)).HidesAll)
            {
                hidden.Add(survey.ItemId);

                continue;
            }

            if (await groups.HiddenGroupsAsync(survey, cancellationToken) is not { } small
                || _hiddenGroups.Count + small.Count > SurveyResponseGroupFilter.MaxGroups)
            {
                hidden.Add(survey.ItemId);

                continue;
            }

            _hiddenGroups.AddRange(small);
        }

        return hidden;
    }

    // The surveys with responses waiting for a grader, for the list of every course.
    private async Task<IReadOnlyList<Survey>> PendingSurveysAsync(Dictionary<string, Survey> known, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<SurveyResultsQueries>() is not { } results)
        {
            return [];
        }

        var pending = await results.ListSurveysPendingGradingAsync(cancellationToken);

        // Summaries from the survey index, a chunk at a time, rather than every pending survey's document.
        if (_serviceProvider.GetService<SurveyIndexReader>() is { } reader)
        {
            var summaries = await reader.ListAsync(pending, publishedOnly: false, cancellationToken);

            foreach (var summary in summaries)
            {
                known[summary.ItemId] = summary;
            }

            return summaries;
        }

        var surveys = new List<Survey>();

        foreach (var surveyId in pending)
        {
            var survey = await _surveyManager.FindByIdAsync(surveyId, cancellationToken);

            known[surveyId] = survey;

            if (survey is not null)
            {
                surveys.Add(survey);
            }
        }

        return surveys;
    }

    private async Task<string> SiteTimeZoneAsync()
        => _serviceProvider.GetService<ISiteService>() is { } siteService ? (await siteService.GetSiteSettingsAsync())?.TimeZoneId : null;

    private static TimeZoneInfo CalendarTimeZone(string timeZoneId)
    {
        if (!string.IsNullOrEmpty(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
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
