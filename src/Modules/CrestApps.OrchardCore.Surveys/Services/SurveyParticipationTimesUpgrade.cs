using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Clears the exact completion times kept on the participation records of anonymous surveys, which keep the day only.
/// It reads and saves documents through YesSql and changes no schema.
/// </summary>
internal static class SurveyParticipationTimesUpgrade
{
    private const string AnonymousKeyPrefix = "a:";

    /// <summary>
    /// Clears the exact completion time of every anonymous participation record.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <returns>A task that completes when the records are saved.</returns>
    public static async Task ClearAnonymousCompletionTimesAsync(ISession session)
    {
        var anonymous = await session.Query<SurveyParticipation, SurveyParticipationIndex>(index => index.RespondentKey.StartsWith(AnonymousKeyPrefix), collection: SurveysConstants.CollectionName)
            .ListAsync();

        foreach (var row in anonymous)
        {
            if (row.LastCompletedUtc is not null)
            {
                row.LastCompletedUtc = null;
                await session.SaveAsync(row, collection: SurveysConstants.CollectionName);
            }
        }
    }
}
