using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Learning.Core.Surveys;

/// <summary>
/// Decides, for the upgrades of first-release course items, whether what an item still holds of the first release (its
/// questions or quiz settings) may be written to its survey.
/// </summary>
public static class LegacySurveyContent
{
    /// <summary>
    /// Gets whether a survey already holds content newer than an item's first-release data: it was published, or its
    /// draft changed well after the survey was made (course staff built it in the designer). The first-release data never
    /// replaces such a survey's draft or settings; it only fills a survey made for it.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns><see langword="true"/> when the first-release data must not overwrite the survey.</returns>
    public static bool IsNewerThanLegacy(Survey survey)
        => survey is not null
            && (survey.PublishedVersionNumber > 0
                || (survey.DraftModifiedUtc is { } modified && survey.CreatedUtc != default && modified - survey.CreatedUtc > TimeSpan.FromMinutes(1)));
}
