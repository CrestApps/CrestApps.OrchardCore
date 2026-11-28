using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// A row a response gives several of (an answer value, a category score, a page visit), expanded once the response's
/// version is known.
/// </summary>
public sealed class SurveyResponseChildRow
{
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the values of the row, by field name.
    /// </summary>
    public Dictionary<string, object> Values { get; set; } = new(StringComparer.Ordinal);
}
