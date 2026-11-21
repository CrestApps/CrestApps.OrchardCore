using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// One response, with the values worked out for it before it is read: names and the fields of providers.
/// </summary>
public sealed class SurveyResponseDataRow
{
    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the survey, or <see langword="null"/>.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets whether the row may show who responded.
    /// </summary>
    public bool ShowsIdentity { get; set; }

    /// <summary>
    /// Gets or sets the user name of the respondent, or <see langword="null"/>.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the name of the ending used, or <see langword="null"/>.
    /// </summary>
    public string Ending { get; set; }

    /// <summary>
    /// Gets or sets the name of the collector, or <see langword="null"/>.
    /// </summary>
    public string Collector { get; set; }

    /// <summary>
    /// Gets the fields of providers, by field name.
    /// </summary>
    public Dictionary<string, object> Extra { get; } = new(StringComparer.Ordinal);
}
