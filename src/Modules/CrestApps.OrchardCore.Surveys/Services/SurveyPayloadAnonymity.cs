using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Reads what a webhook or workflow payload of an anonymous response may carry: its collector, version and context kind
/// only when enough completed responses share their joint key (<see cref="SurveyAnonymousAttributes"/>, the rule every
/// other output follows), and its days in the site's time zone (<see cref="SurveyAnonymousDays"/>).
/// </summary>
internal static class SurveyPayloadAnonymity
{
    /// <summary>
    /// Reads what a payload of a response may carry.
    /// </summary>
    /// <param name="services">The services of the scope.</param>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <param name="response">The response.</param>
    /// <returns>What it shows of its attributes and the time zone of its days; <see langword="null"/> for both on an
    /// identified survey.</returns>
    public static async Task<(SurveyRowAttributes? Shows, TimeZoneInfo TimeZone)> ReadAsync(IServiceProvider services, Survey survey, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(response);

        if (!SurveyAnonymity.IsAnonymous(survey))
        {
            return (null, null);
        }

        // Without the small-group rule nothing can be counted, so nothing of the three shows.
        var shows = services.GetService<SurveyAnonymousGroups>() is { } groups
            ? (await groups.AttributesAsync(survey)).Of(response)
            : new SurveyRowAttributes(false, false, false, false);
        var timeZone = await (services.GetService<SurveyAnonymousDays>() ?? new SurveyAnonymousDays(services)).TimeZoneAsync();

        return (shows, timeZone);
    }
}
