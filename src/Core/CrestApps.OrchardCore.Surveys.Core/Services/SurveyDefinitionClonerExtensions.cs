using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Copies the settings records of a survey.
/// </summary>
public static class SurveyDefinitionClonerExtensions
{
    /// <summary>
    /// Makes a deep copy of access settings.
    /// </summary>
    /// <param name="access">The settings.</param>
    /// <returns>The copy.</returns>
    public static SurveyAccessSettings CloneAccess(SurveyAccessSettings access)
        => access is null ? new SurveyAccessSettings() : JsonSerializer.Deserialize<SurveyAccessSettings>(JsonSerializer.Serialize(access));
}
