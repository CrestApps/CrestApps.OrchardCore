using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Hears when a quota of a survey reaches its limit, such as the workflow event.
/// </summary>
public interface ISurveyQuotaObserver
{
    /// <summary>
    /// A quota reached its limit.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="quota">The quota.</param>
    /// <param name="count">The completed count of the quota.</param>
    /// <returns>A task.</returns>
    Task QuotaReachedAsync(Survey survey, SurveyQuota quota, int count);
}
