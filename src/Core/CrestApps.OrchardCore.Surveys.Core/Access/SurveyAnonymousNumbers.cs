using CrestApps.OrchardCore.Surveys.Core.Indexes;
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
    /// Clears the number of every response of a survey, a page at a time.
    /// </summary>
    /// <param name="session">The session the responses are saved in.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many responses lost their number.</returns>
    public static async Task<int> ClearAsync(ISession session, string surveyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        var cleared = new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            // Each page is saved before the next is read (a query flushes the session first), so the responses saved no
            // longer match; the ids already cleared stop the walk should a save not reach the index.
            var page = (await session.Query<SurveyResponse, SurveyResponseIndex>(index => index.SurveyId == surveyId && index.Number != null, collection: SurveysConstants.ResponsesCollectionName)
                .OrderBy(index => index.ItemId)
                .Take(SurveysConstants.PageSize)
                .ListAsync(cancellationToken))
                .Where(response => !cleared.Contains(response.ItemId ?? string.Empty))
                .ToList();

            if (page.Count == 0)
            {
                return cleared.Count;
            }

            foreach (var response in page)
            {
                response.Number = null;
                cleared.Add(response.ItemId ?? string.Empty);
                await session.SaveAsync(response, collection: SurveysConstants.ResponsesCollectionName, cancellationToken: cancellationToken);
            }

            await session.FlushAsync(cancellationToken);
        }
    }
}
