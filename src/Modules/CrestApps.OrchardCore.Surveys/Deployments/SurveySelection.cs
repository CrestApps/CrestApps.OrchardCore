using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Reads the surveys a deployment step selects.
/// </summary>
internal static class SurveySelection
{
    /// <summary>
    /// Lists the selected surveys.
    /// </summary>
    /// <param name="store">The survey store.</param>
    /// <param name="includeAll">Whether every survey is selected.</param>
    /// <param name="names">The technical names selected otherwise.</param>
    /// <returns>The surveys.</returns>
    public static async Task<IReadOnlyList<Survey>> ListAsync(SurveyStore store, bool includeAll, IEnumerable<string> names)
    {
        if (includeAll)
        {
            return (await store.GetAllAsync()).ToList();
        }

        var surveys = new List<Survey>();

        foreach (var name in (names ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await store.FindByNameAsync(name) is { } survey)
            {
                surveys.Add(survey);
            }
        }

        return surveys;
    }
}
