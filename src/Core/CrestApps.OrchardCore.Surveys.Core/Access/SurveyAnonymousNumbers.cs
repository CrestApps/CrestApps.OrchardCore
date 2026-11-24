using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Clears the numbers of a survey's responses when the survey becomes anonymous. Response numbers come from one sequence
/// for the whole site, in the order responses start, so next to a named response's number an anonymous response's number
/// would tell who gave it; responses started while a survey was anonymous never get one.
/// </summary>
public static class SurveyAnonymousNumbers
{
    /// <summary>
    /// Clears the number of every response of a survey, a page at a time: each page is saved through the response store
    /// (with its concurrency check), written, and let go of before the next is read, so a large survey is never held in
    /// memory at once.
    /// </summary>
    /// <param name="responses">The response store the responses are saved through.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many responses lost their number.</returns>
    public static async Task<int> ClearAsync(SurveyResponseStore responses, string surveyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responses);

        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        var session = responses.YesSqlSession;

        // Saving writes a response's index row again under a new id, so the walk stops at the last row there when it
        // began: a saved response is never read twice.
        var last = await session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId, collection: SurveysConstants.ResponsesCollectionName)
            .OrderByDescending(index => index.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var upToId = last?.Id ?? 0;
        var afterId = 0L;
        var cleared = 0;

        while (afterId < upToId)
        {
            var from = afterId;
            var rows = (await session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Number != null && index.Id > from && index.Id <= upToId, collection: SurveysConstants.ResponsesCollectionName)
                .OrderBy(index => index.Id)
                .Take(SurveysConstants.PageSize)
                .ListAsync(cancellationToken))
                .ToList();

            if (rows.Count == 0)
            {
                break;
            }

            afterId = rows[^1].Id;

            var page = (await responses.GetAsync(rows.Select(row => row.ItemId).ToArray(), cancellationToken))
                .Where(response => response is not null)
                .ToList();

            foreach (var response in page.Where(response => response.Number is not null))
            {
                response.Number = null;
                await responses.UpdateAsync(response, cancellationToken);
                cleared++;
            }

            await session.FlushAsync(cancellationToken);

            foreach (var response in page)
            {
                session.Detach(response, SurveysConstants.ResponsesCollectionName);
            }
        }

        return cleared;
    }
}
